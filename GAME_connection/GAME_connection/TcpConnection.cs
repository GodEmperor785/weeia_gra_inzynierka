﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace GAME_connection {
	/// <summary>
	/// Class used to send and receive GamePackets between server and client.
	/// <para>Should be used instead of TcpClient and its NetworkStream, if you need to have same connection in multiple places PASS instance of this object, DON'T create new object.</para>
	/// <para>All send and receive operations are synchronized separately (one lock for send and one for receive)</para>
	/// <para>Receive operations are preformed on separate thread to allow constant connection testing</para>
	/// <para>To disconnect from remote use <see cref="SendDisconnect"/>. To process remote disconnect use <see cref="Disconnect"/></para>
	/// </summary>
	public class TcpConnection : IDisposable {
		public static readonly int DEFAULT_PORT = 10001;

		private static readonly int connectionTestIntervalMilis = 5000;

		private TcpClient tcpClient;
		private NetworkStream netStream;
		private IFormatter serializer;
		private string remoteIpAddress;
		private int remotePortNumber;

		private readonly object sendLock = new object();
		private readonly object receiveLock = new object();
		private readonly object queueLock = new object();
		private readonly object keepReceivingLock = new object();
		private readonly object keepTestingConnectionLock = new object();
		private readonly object alreadyDisconnectedLock = new object();

		private Queue<GamePacket> receivedPackets = new Queue<GamePacket>();
		private Thread receiver;
		private Thread connectionTester = null;	//thread to periodically test connection - will be used on client - true in constructor
		private bool keepReceiving;
		private bool keepTestingConnection;
		private AutoResetEvent messageReceivedEvent;
		private AutoResetEvent connectionEndedEvent;

		private bool remotePlannedDisconnect;
		private bool connectionEnded;
		private bool alreadyDisconnected;

		#region Constructor
		/// <summary>
		/// Creates all necessary variables and threads for game connection, requires connected <see cref="System.Net.Sockets.TcpClient"/>.
		/// </summary>
		/// <param name="client">connected <see cref="System.Net.Sockets.TcpClient"/></param>
		/// <param name="isClient">true if used on the client side - clients send  <see cref="OperationType.CONNECTION_TEST"/> packets to server</param>
		public TcpConnection(TcpClient client, bool isClient) {
			this.TcpClient = client;
			this.NetStream = client.GetStream();

			IPEndPoint ipData = client.Client.RemoteEndPoint as IPEndPoint;
			this.RemoteIpAddress = ipData.Address.ToString();
			this.RemotePortNumber = ipData.Port;
			this.serializer = new BinaryFormatter();
			this.messageReceivedEvent = new AutoResetEvent(false);
			this.connectionEndedEvent = new AutoResetEvent(false);

			alreadyDisconnected = false;
			keepReceiving = true;
			RemotePlannedDisconnect = false;
			receiver = new Thread(new ThreadStart(DoReceiving));
			receiver.Start();

			if(isClient) {
				keepTestingConnection = true;
				connectionTester = new Thread(new ThreadStart(DoTestConnection));
				connectionTester.Start();
			}
		}
		#endregion

		#region Send/Write
		/// <summary>
		/// Used to send <see cref="GamePacket"/> to client specified in constructor (TcpClient)
		/// </summary>
		/// <param name="packet">instance of <see cref="GamePacket"/> object to send</param>
		public void Send(GamePacket packet) {
			if (connectionEnded) throw new ConnectionEndedException("Trying to send when connection is closed", "send");
			try {
				lock (sendLock) {
					serializer.Serialize(netStream, packet);
				}
			} catch (IOException ex) {
				Console.WriteLine("Connection ended");
				//Console.WriteLine(ex.StackTrace);
				connectionEnded = true;
			} catch (SerializationException ex2) {
				Console.WriteLine("Connection ended");
				//Console.WriteLine(ex2.StackTrace);
				connectionEnded = true;
			}
		}
		#endregion

		#region Receive/Read
		/// <summary>
		/// Waits and gets oldest unprocessed received packet. Waits without timeout.
		/// </summary>
		/// <returns></returns>
		public GamePacket GetReceivedPacket() {
			int queueCount;
			messageReceivedEvent.WaitOne();     //wait until there is a message
			queueCount = QueueCount;
			if (connectionEnded && queueCount == 0) throw new ConnectionEndedException("Trying to receive when connection is closed", "receive");
			lock (queueLock) {
				return receivedPackets.Dequeue();
			}
		}

		/// <summary>
		/// Waits and gets oldest unprocessed received packet with timeout in miliseconds, on timeout throws exception
		/// </summary>
		/// <param name="timeoutMilis">timeout in miliseconds for receive operation</param>
		/// <param name="playerNumber">number of player that had timeout</param>
		/// <returns></returns>
		public GamePacket GetReceivedPacket(int timeoutMilis, int playerNumber) {
			int queueCount;
			messageReceivedEvent.WaitOne(timeoutMilis);     //wait until there is a message with timeout
			queueCount = QueueCount;
			if (queueCount > 0) {
				lock (queueLock) {
					return receivedPackets.Dequeue();
				}
			}
			else if (queueCount == 0 && connectionEnded) throw new ConnectionEndedException("Trying to receive when connection is closed", "receive with timeout");
			else throw new ReceiveTimeoutException(playerNumber);
		}

		private int QueueCount {
			get {
				int queueCount;
				lock (queueLock) {
					queueCount = receivedPackets.Count;
				}
				return queueCount;
			}
		}
		#endregion

		#region Receiver
		/// <summary>
		/// Main receiving thread, receives <see cref="GamePacket"/>s and puts them on the queue, ignores <see cref="OperationType.CONNECTION_TEST"/> packets
		/// </summary>
		private void DoReceiving() {
			while(KeepReceiving) {
				try {
					GamePacket receivedPacket = Receive();
					if (receivedPacket.OperationType == OperationType.CONNECTION_TEST) Console.WriteLine("CONNECTION_TEST received");
					else {
						lock (queueLock) {
							receivedPackets.Enqueue(receivedPacket);
							messageReceivedEvent.Set();
						}
					}
					if (receivedPacket.OperationType == OperationType.DISCONNECT) KeepReceiving = false;        //stop receiving if disconnect
				} catch(IOException ex) {
					Console.WriteLine("Connection ended");
					//Console.WriteLine(ex.StackTrace);
					connectionEnded = true;
					break;
				} catch (SerializationException ex2) {
					Console.WriteLine("Connection ended");
					//Console.WriteLine(ex2.StackTrace);
					connectionEnded = true;
					break;
				}
				//than block on another read operation
			}
			if(AlreadyDisconnected) this.Disconnect();
		}

		/// <summary>
		/// Used to receive <see cref="GamePacket"/>s
		/// </summary>
		/// <returns>received (deserialized) GamePacket</returns>
		private GamePacket Receive() {
			if (connectionEnded) throw new ConnectionEndedException("Trying to receive when connection is closed", "receive");
			lock (receiveLock) {
				return (GamePacket)serializer.Deserialize(netStream);
			}
		}

		private bool KeepReceiving {
			get {
				bool localKeepReceiving;
				lock (keepReceivingLock) {
					localKeepReceiving = keepReceiving;
				}
				return localKeepReceiving;
			}
			set {
				lock (keepReceivingLock) {
					keepReceiving = value;
				}
			}
		}
		#endregion

		#region Connection Tester
		/// <summary>
		/// Periodically tests connection, used on client
		/// </summary>
		private void DoTestConnection() {
			while(KeepTestingConnection) {
				try {
					if(!connectionEnded) Send(GamePacket.CreateConnectionTestPacket());
					connectionEndedEvent.WaitOne(connectionTestIntervalMilis);
					//Thread.Sleep(connectionTestIntervalMilis);
				} catch (IOException ex) {
					Console.WriteLine("Connection ended");
					//Console.WriteLine(ex.StackTrace);
					connectionEnded = true;
					break;
				} catch (SerializationException ex2) {
					Console.WriteLine("Connection ended");
					//Console.WriteLine(ex2.StackTrace);
					connectionEnded = true;
					break;
				}
			}
		}

		private bool KeepTestingConnection {
			get {
				bool localKeepTestingConnection;
				lock (keepTestingConnectionLock) {
					localKeepTestingConnection = keepTestingConnection;
				}
				return localKeepTestingConnection;
			}
			set {
				lock (keepTestingConnectionLock) {
					keepTestingConnection = value;
				}
			}
		}
		#endregion

		public TcpClient TcpClient { get => tcpClient; set => tcpClient = value; }
		public NetworkStream NetStream { get => netStream; set => netStream = value; }
		public string RemoteIpAddress { get => remoteIpAddress; set => remoteIpAddress = value; }
		public int RemotePortNumber { get => remotePortNumber; set => remotePortNumber = value; }
		public bool RemotePlannedDisconnect { get => remotePlannedDisconnect; set => remotePlannedDisconnect = value; }

		#region IDisposable and Disconnect
		private void ProcessDisconnectInternal(bool sendDisconnectToRemote) {
			if(sendDisconnectToRemote) Send(new GamePacket(OperationType.DISCONNECT, new object()));
			RemotePlannedDisconnect = true;
			KeepTestingConnection = false;
			KeepReceiving = false;
			messageReceivedEvent.Set();
			connectionEndedEvent.Set();
		}

		private bool AlreadyDisconnected {
			get {
				bool localAlreadyDisconnected;
				lock (alreadyDisconnectedLock) {
					localAlreadyDisconnected = alreadyDisconnected;
				}
				return localAlreadyDisconnected;
			}
			set {
				lock (alreadyDisconnectedLock) {
					alreadyDisconnected = value;
				}
			}
		}

		/// <summary>
		/// Use this method to send proper disconnect to remote. DO NOT send packet <see cref="OperationType.CONNECTION_TEST"/> manually and call Dispose or Disconnect!
		/// </summary>
		public void SendDisconnect() {
			ProcessDisconnectInternal(true);
			Thread.Sleep(100);		//give threads some time to process disconnect
			this.Dispose();
		}

		/// <summary>
		/// Use this method when you receive <see cref="OperationType.CONNECTION_TEST"/> packet from remote. DO NOT use it when you want to SEND <see cref="OperationType.CONNECTION_TEST"/>
		/// </summary>
		public void Disconnect() {
			ProcessDisconnectInternal(false);
			this.Dispose();
		}

		/// <summary>
		/// called internally by proper <see cref="Disconnect"/> and <see cref="SendDisconnect"/>. SHOULDN'T be used manually!
		/// </summary>
		public void Dispose() {
			TcpClient.Dispose();

			//Console.WriteLine("0");
			if (connectionTester != null) connectionTester.Join();
			//Console.WriteLine("1");
			if (receiver.ManagedThreadId != Thread.CurrentThread.ManagedThreadId) receiver.Join();
			//Console.WriteLine("2");
			AlreadyDisconnected = true;
		}
		#endregion

	}

}
