using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using DemoShared;
using TcpInteract;
using UnityEngine;

namespace DemoClient
{
    /// <summary>
    /// Represents a TCP client used for instant messaging.
    /// </summary>
    public class MessengerClient : ClientSideClient
    {
        private readonly BindingList<ClientCursorContent> clientCursorPositions = new BindingList<ClientCursorContent>();
        public IBindingList ClientCursorPositions => clientCursorPositions;
        public delegate void PlayerChanged(string name, int reason);
        public event PlayerChanged OnPlayerChanged;
        //{{ 2020-11-26_jintaeks
        public delegate void NewCursorPosition(string name, Point pos);
        public event NewCursorPosition OnNewCursorPosition;
        //}} 2020-11-26_jintaeks

        public MessengerClient()
        {
        }

        public bool CanSendPackage()
        {
            return (Socket != null && Socket.Connected && Status == ClientStatus.LoggedIn);
        }

        /// <summary>
        /// Sends an instant message asynchronously.
        /// </summary>
        public void SendMessageAsync(string message)
        {
            SendPackageAsyncBase((int)Commands.InstantMessage, new InstantMessageContent(message, Name));
        }

        public void SendPackageAsync(int command, ISerializable serializable)
        {
            SendPackageAsyncBase(command, serializable);
        }

        protected override void OnLoggedOut(LogoutContent content)
        {
            base.OnLoggedOut(content);
            RemoveClientCursorPosition(content.ClientName);
        }

        private void RemoveClientCursorPosition(string clientName)
        {
            var find = clientCursorPositions.FirstOrDefault(c => c.ClientName == clientName);

            if (find != null)
            {
                clientCursorPositions.Remove(find);
                if (OnPlayerChanged != null )
                    OnPlayerChanged(clientName, 0);
            }
        }

        protected override void OnPackageReceived(Package package)
        {
            base.OnPackageReceived(package);

            switch ((Commands)package.Command)
            {
                case Commands.InstantMessage:
                    Pusher.Push(InstantMessageContent.Deserialize(package.Content));
                    break;

                case Commands.Screenshot:
                    Pusher.Push(ScreenshotContent.Deserialize(package.Content));
                    break;

                case Commands.CursorPosition:
                    var args = ClientCursorContent.Deserialize(package.Content);
                    var find = clientCursorPositions.FirstOrDefault(c => c.ClientName == args.ClientName);

                    if (find == null)
                    {
                        clientCursorPositions.Add(args);
                        if (OnPlayerChanged != null)
                            OnPlayerChanged(args.ClientName, 1);
                    }
                    else
                    {
                        int index = clientCursorPositions.IndexOf(find);

                        if (index != -1)
                        {
                            clientCursorPositions[index] = args;
                            //{{ 2020-11-26_jintaeks
                            if (OnNewCursorPosition != null)
                                OnNewCursorPosition(args.ClientName, args.CursorPosition);
                            //}} 2020-11-26_jintaeks
                        }
                    }
                    break;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
