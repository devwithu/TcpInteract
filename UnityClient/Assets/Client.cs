using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Drawing;
using UnityEngine;
using UnityEngine.AI;
using TcpInteract;
using DemoShared;
using DemoClient;

public class Client : MonoBehaviour
{
    private MessengerClient client = null;
    private string _serverIp = "127.0.0.1";
    private const int PORT = 2059;
    private string _clientName = "client1";
    private bool _isSyncPosition = true;
    private string _chatMessage;
    [SerializeField]
    private GameObject _playerPrefab;
    private Vector3 _position; // local player position

    // Start is called before the first frame update
    void Start()
    {
        client = new MessengerClient();
        LoadSettings();

        client.StatusChanged += ClientStatusChanged;
        client.Pusher.Bind<ServerClosedContent>(content =>
        {
            Debug.Log("The server has closed.");
        });
        client.Pusher.Bind<ConnectionRefusedContent>(ClientOnConnectionRefused);
        client.Pusher.Bind<LoginContent>(ClientOnClientLoggedIn);
        client.Pusher.Bind<LogoutContent>(ClientOnClientLoggedOut);
        client.Pusher.Bind<InstantMessageContent>(ClientOnInstantMessage);
        client.OnPlayerChanged += OnPlayerChanged;
        //{{ 2020-11-26_jintaeks
        client.OnNewCursorPosition += OnNewCursorPosition;
        //}} 2020-11-26_jintaeks

        StartCoroutine(SyncPosition());
    }

    // Update is called once per frame
    void Update()
    {
	    //{{ 2020-11-26_jintaeks
        // qff. client position must be updated here. 20201118_jintaeks
        GameObject go = GameObject.Find(_clientName);
        if (go)
            _position = go.transform.position;
		//}} 2020-11-26_jintaeks
    }

    private void OnDestroy()
    {
        client.Logout();
        client.Dispose();
    }

    private static void ShowErrorMessage(string message)
    {
        Debug.Log(message);
    }

    private void LoadSettings()
    {
    }

    private void ClientStatusChanged(object sender, EventArgs e)
    {
        switch (client.Status)
        {
            case ClientStatus.Connected:
                Debug.Log("Connected. Awaiting login approval.");
                break;

            case ClientStatus.Disconnected:
                Debug.Log("Idle.");
                break;

            case ClientStatus.LoggedIn:
                Debug.Log("Logged in." + client.Name);
                client.Synchronize();
                break;
        }
    }

    private void ClientOnClientLoggedIn(LoginContent content)
    {
        Debug.Log($@"{content.ClientName}: logged in.");
		//{{ 2020-11-26_jintaeks
        GameObject go = GameObject.Find(content.ClientName);
        if (go == null)
        {
            go = Instantiate(_playerPrefab
                , new Vector3(UnityEngine.Random.Range(-5.0f, +5.0f), 1.0f, UnityEngine.Random.Range(-5.0f, +5.0f))
                , Quaternion.identity);
            go.name = content.ClientName;
            if(_clientName == content.ClientName)
                go.tag = "Player";

            ClientCursorContent cursorContent = new ClientCursorContent( new Point(0,0), content.ClientName);
            client.ClientCursorPositions.Add( cursorContent);
        }
		//}} 2020-11-26_jintaeks
    }

    private void ClientOnClientLoggedOut(LogoutContent content)
    {
        string message;

        switch (content.Reason)
        {
            case LogoutReason.Kicked:
                message = $@"{content.ClientName}: was kicked. Reason: {content.Message}";

                if (content.ClientName == client.Name)
                {
                    Debug.Log($"You have been kicked.\nReason: {content.Reason}.");
                }
                break;

            case LogoutReason.TimedOut:
                message = $@"{content.ClientName}: timed out.";
                break;

            case LogoutReason.UserSpecified:
                message = $@"{content.ClientName}: logged out.";
                break;

            default:
                throw new InvalidEnumArgumentException();
        }

        Debug.Log(message);
    }

    private void ClientOnConnectionRefused(ConnectionRefusedContent e)
    {
        Debug.Log("Connection refused: " + e.Reason);
    }

    private void RequestLogin()
    {
        IPAddress address;

        try
        {
            address = IPAddress.Parse(_serverIp);
        }
        catch (FormatException)
        {
            ShowErrorMessage("Invalid address format.");
            return;
        }

        client.Name = _clientName;
        client.EndPoint = new IPEndPoint(address, PORT);

        try
        {
            client.RequestLogin();
        }
        catch (InvalidOperationException ex)
        {
            Debug.Log(ex.Message);
        }
        catch (AlreadyLoggedInException ex)
        {
            Debug.Log(ex.Message);
        }
    }
    private void ClientOnInstantMessage(InstantMessageContent content)
    {
        Debug.Log(content.Message);
        _chatMessage += content.Message;
        _chatMessage += "\n";
    }

    IEnumerator SyncPosition()
    {
        while (_isSyncPosition)
        {
            if (client.CanSendPackage())
            {
                Point p = new Point((int)_position.x, (int)_position.z);
                var args = new ClientCursorContent(p, client.Name);
                client.SendPackageAsync((int)Commands.CursorPosition, args);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void OnPlayerChanged(string name, int reason)
    {
        if (reason == 0)
        {
            GameObject go = GameObject.Find(name);
            GameObject.Destroy(go);
        }
        else if (reason == 1)
        {
            GameObject go = Instantiate(_playerPrefab
                , new Vector3(UnityEngine.Random.Range(-5.0f, +5.0f), 1.0f, UnityEngine.Random.Range(-5.0f, +5.0f))
                , Quaternion.identity);
            go.name = name;
        }
    }

    //{{ 2020-11-26_jintaeks
    void OnNewCursorPosition(string name, Point newPos)
    {
        GameObject go = GameObject.Find(name);
        if (go != null && go.tag != "Player")
        {
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            Vector3 pos = go.transform.position;
            pos.x = newPos.X;
            pos.z = newPos.Y;
            //go.transform.position = pos;
            agent.SetDestination(pos);
        }
    }
	//}} 2020-11-26_jintaeks

    private void OnGUI()
    {
        _clientName = GUI.TextField(new Rect(0, 0, 160, 30), _clientName);
        _serverIp = GUI.TextField(new Rect(0, 30, 160, 30), _serverIp);
        if( GUI.Button(new Rect(0,60,80,30),"Connect") )
        {
            RequestLogin();
        }
        GUI.Label(new Rect(0, 90, 320, 200), _chatMessage);
    }
}
