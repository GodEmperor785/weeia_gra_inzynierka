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
using System.Security.Principal;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Security.Authentication;

namespace GAME_connection {
	/// <summary>
	/// Class used to send and receive GamePackets between server and client.
	/// <para>Should be used instead of TcpClient and its NetworkStream, if you need to have same connection in multiple places PASS instance of this object, DON'T create new object.</para>
	/// <para>All send and receive operations are synchronized separately (one lock for send and one for receive)</para>
	/// <para>Receive operations are preformed on separate thread to allow constant connection testing</para>
	/// <para>To disconnect from remote use <see cref="SendDisconnect"/>. To process remote disconnect use <see cref="Disconnect"/></para>
	/// </summary>
	public class TcpConnection : IDisposable {
		public static readonly int DEFAULT_PORT = 10000;
		public static readonly int DEFAULT_PORT_CLIENT = 51410;

		private static readonly int connectionTestIntervalMilis = 5000;

		Random rid = new Random();

		private TcpClient tcpClient;
		//private NetworkStream netStream;
		private Stream netStream;
		private IFormatter serializer;
		private string remoteIpAddress;
		private int remotePortNumber;
		private X509Certificate serverCertificateObject;
		private string serverCertificatePublicKey;

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

		public delegate void Logger(string message);
		private bool debug;
		public bool fullDebug;
		internal Logger tcpConnectionLogger;

		private int playerNumber = 0;

		public event EventHandler GameAbandoned;
		/// <summary>
		/// this event is used when sudden disconnect has taken place - disconnect that was not planned by client sending <see cref="OperationType.DISCONNECT"/> message to server
		/// </summary>
		public event EventHandler<GameEventArgs> ConnectionEnded;
		public event EventHandler<GameEventArgs> Surrender;
		public event EventHandler ConnectionTestReceived;
		public event EventHandler GameOver;			//might be used for client when other player suddenly ends game

		#region Constructor
		/// <summary>
		/// Creates all necessary variables and threads for game connection, requires connected <see cref="System.Net.Sockets.TcpClient"/>.
		/// </summary>
		/// <param name="client">connected <see cref="System.Net.Sockets.TcpClient"/></param>
		/// <param name="isClient">true if used on the client side - clients send  <see cref="OperationType.CONNECTION_TEST"/> packets to server</param>
		/// <param name="logger">method to log messages in this object</param>
		/// <param name="printDebugInfo">prints debug info to console if <see langword="true"/></param>
		/// <param name="useSSL">if <see langword="true"/> uses <see cref="SslStream"/> instead of bare <see cref="NetworkStream"/>, defaults to <see langword="false"/></param>
		/// <param name="sslCertificatePath"> specifies path to .cer file containing servers certificate</param>
		public TcpConnection(TcpClient client, bool isClient, Logger logger, bool printDebugInfo = true, bool useSSL = false, string sslCertificatePath = null) {
			this.TcpClient = client;
			try {
				tcpConnectionLogger = logger;
				debug = printDebugInfo;
				if (useSSL) {
					if (!isClient && !SslUtils.IsAdministrator()) throw new NotAdministratorException("You need to run server application as local administartor if you want to use SSL!");
					if (!isClient) {
						serverCertificateObject = SslUtils.LoadServerCertificate(sslCertificatePath, printDebugInfo, logger);
						SslStream sslStream = new SslStream(client.GetStream(), false);
						sslStream.AuthenticateAsServer(serverCertificateObject);
						this.NetStream = sslStream;
					}
					else {
						PublicKeys.SetUsedPublicKey(PublicKeys.SERVER_CERTIFICATE_PUBLIC_KEY_STRING_SERVER);   //modify this in order to change location of server application
						SslStream sslStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(SslUtils.ValidateServerCertificateNoImport), null);
						sslStream.AuthenticateAsClient(PublicKeys.PublicKeysToServerName[PublicKeys.USED_PUBLIC_KEY]);
						this.NetStream = sslStream;
					}
				}
				else {
					this.NetStream = client.GetStream();
				}

				IPEndPoint ipData = client.Client.RemoteEndPoint as IPEndPoint;
				this.RemoteIpAddress = ipData.Address.ToString();
				this.RemotePortNumber = ipData.Port;
				this.serializer = new BinaryFormatter();		//BinaryFormatter ALWAYS uses little endian
				this.messageReceivedEvent = new AutoResetEvent(false);
				this.connectionEndedEvent = new AutoResetEvent(false);

				alreadyDisconnected = false;
				keepReceiving = true;
				RemotePlannedDisconnect = false;
				receiver = new Thread(new ThreadStart(DoReceiving));
				receiver.Start();

				if (isClient) {
					keepTestingConnection = true;
					connectionTester = new Thread(new ThreadStart(DoTestConnection));
					connectionTester.Start();
				}
			} catch(AuthenticationException) {
				client.Close();
				throw;
			} catch(IOException) {
				client.Close();
				throw;
			} catch(Exception) {
				client.Close();
				throw;
			}
		}

		#endregion

		#region info
		public void PrintSecurityInfo() {
			if (this.NetStream is SslStream) {
				SslUtils.PrintSslInfo(this.NetStream as SslStream, tcpConnectionLogger);
			}
			else {
				tcpConnectionLogger("Connection is not using SSL/TLS");
			}
		}
		#endregion

		#region Creator methods
		private static void Log(string message) {
			Console.WriteLine(message);
		}

		public static TcpConnection CreateDefaultTcpConnectionForClient(TcpClient client) {
			return new TcpConnection(client, true, Log);
		}
		
		public static TcpConnection CreateDefaultTcpConnectionForServer(TcpClient client) {
			return new TcpConnection(client, false, Log);
		}

		public static TcpConnection CreateDefaultTcpConnectionForClientNoDebug(TcpClient client) {
			return new TcpConnection(client, true, Log, printDebugInfo: false);
		}

		public static TcpConnection CreateDefaultTcpConnectionForServerNoDebug(TcpClient client) {
			return new TcpConnection(client, false, Log, printDebugInfo: false);
		}

		public static TcpConnection CreateCustomLoggerTcpConnectionForClient(TcpClient client, Logger logger) {
			return new TcpConnection(client, true, logger);
		}

		public static TcpConnection CreateCustomLoggerTcpConnectionForServer(TcpClient client,  Logger logger) {
			return new TcpConnection(client, false, logger);
		}

		public static TcpConnection CreateCustomLoggerTcpConnectionForClientNoDebug(TcpClient client, Logger logger) {
			return new TcpConnection(client, true, logger, printDebugInfo: false);
		}

		public static TcpConnection CreateCustomLoggerTcpConnectionForServerNoDebug(TcpClient client, Logger logger) {
			return new TcpConnection(client, false, logger, printDebugInfo: false);
		}

		public static TcpConnection CreateSslTcpConnectionForClient(TcpClient client, Logger logger, bool printDebug) {
			return new TcpConnection(client, true, logger, printDebugInfo: printDebug, useSSL: true);
		}

		public static TcpConnection CreateSslTcpConnectionForServer(TcpClient client, Logger logger, bool printDebug, string certificatePath) {
			return new TcpConnection(client, false, logger, printDebugInfo: printDebug, useSSL: true, sslCertificatePath: certificatePath);
		}
		#endregion

		#region Send/Write
		/// <summary>
		/// Used to send <see cref="GamePacket"/> to client specified in constructor (TcpClient)
		/// </summary>
		/// <param name="packet">instance of <see cref="GamePacket"/> object to send</param>
		public void Send(GamePacket packet) {
			if (connectionEnded) throw new ConnectionEndedException("Trying to send when connection is closed", PlayerNumber);
			try {
				lock (sendLock) {
					if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- start sending: " + packet.OperationType + " thread: " + Thread.CurrentThread.Name);
					serializer.Serialize(netStream, packet);
					if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- sent: " + packet.OperationType + " thread: " + Thread.CurrentThread.Name);
				}
			} catch (IOException ex) {
				if(debug) tcpConnectionLogger("Connection ended - on write");
				OnConnectionEnded(new GameEventArgs(PlayerNumber));
				//Console.WriteLine(ex.StackTrace);
				connectionEnded = true;
			} catch (SerializationException ex2) {
				if (debug) tcpConnectionLogger("Connection ended - on write");
				OnConnectionEnded(new GameEventArgs(PlayerNumber));
				//Console.WriteLine(ex2.StackTrace);
				connectionEnded = true;
			} catch(Exception ex3) {
				if (debug) tcpConnectionLogger("Connection ended - on write");
				OnConnectionEnded(new GameEventArgs(PlayerNumber));
				//Console.WriteLine(ex3.StackTrace);
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
			if (connectionEnded && queueCount == 0) {
				if (debug) tcpConnectionLogger("Trying to receive when connection is closed");
				throw new ConnectionEndedException("Trying to receive when connection is closed", PlayerNumber);
			}
			try {
				lock (queueLock) {
					if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- getting packet" + " thread: " + Thread.CurrentThread.Name);
					GamePacket packet = receivedPackets.Dequeue();
					if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- packet got" + " thread: " + Thread.CurrentThread.Name);
					return packet;
				}
			} catch (Exception) {
				throw new ConnectionEndedException("Trying to receive when connection is closed", PlayerNumber);
			}
		}

		/// <summary>
		/// Waits and gets oldest unprocessed received packet with timeout in miliseconds, on timeout returns null
		/// </summary>
		/// <param name="timeoutMilis">timeout in miliseconds for receive operation</param>
		/// <returns></returns>
		public GamePacket GetReceivedPacket(int timeoutMilis) {
			int queueCount;
			if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- starting to wait for packet");
			messageReceivedEvent.WaitOne(timeoutMilis);     //wait until there is a message with timeout
			if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- packet reception event");
			queueCount = QueueCount;
			if (queueCount > 0) {
				try {
					lock (queueLock) {
						if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- getting packet with timeout" + " thread: " + Thread.CurrentThread.Name);
						var packet = receivedPackets.Dequeue();
						if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- packet got" + " thread: " + Thread.CurrentThread.Name);
						return packet;
					}
				}
				catch (Exception) {
					throw new ConnectionEndedException("Trying to receive when connection is closed", PlayerNumber);
				}
			}
			else if (queueCount == 0 && connectionEnded) {
				if (debug) tcpConnectionLogger("Trying to receive when connection is closed");
				throw new ConnectionEndedException("Trying to receive when connection is closed", PlayerNumber);
			}
			else {
				if (debug) tcpConnectionLogger("no packet received in timeout " + timeoutMilis);
				return null;
			}
		}

		public int QueueCount {
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
					if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- do receiving");
					GamePacket receivedPacket = Receive();
					if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- do receiving, got: " + receivedPacket.OperationType);
					if (receivedPacket.OperationType == OperationType.CONNECTION_TEST) OnConnectionTestReceived(EventArgs.Empty);
					else if (receivedPacket.OperationType == OperationType.ABANDON_GAME) OnGameAbandoned(EventArgs.Empty);
					else if (receivedPacket.OperationType == OperationType.GAME_END) {
						EnqueuePacket(receivedPacket);
						OnGameOver(EventArgs.Empty);
					}
					else if (receivedPacket.OperationType == OperationType.SURRENDER) OnSurrender(new GameEventArgs(PlayerNumber));
					else {
						if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- Received packet: " + receivedPacket.OperationType);
						EnqueuePacket(receivedPacket);
						if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- enqueued");
					}
					if (receivedPacket.OperationType == OperationType.DISCONNECT) {
						KeepReceiving = false;        //stop receiving if disconnect
						OnConnectionEnded(new GameEventArgs(PlayerNumber));
					}
				} catch(IOException ex) {
					if (debug) tcpConnectionLogger("Connection ended - on read");
					OnConnectionEnded(new GameEventArgs(PlayerNumber));
					//Console.WriteLine(ex.StackTrace);
					connectionEnded = true;
					break;
				} catch (SerializationException ex2) {
					if (debug) tcpConnectionLogger("Connection ended - on read");
					OnConnectionEnded(new GameEventArgs(PlayerNumber));
					//Console.WriteLine(ex2.StackTrace);
					connectionEnded = true;
					break;
				} catch (Exception ex3) {
					if (debug) tcpConnectionLogger("Connection ended - on read");
					OnConnectionEnded(new GameEventArgs(PlayerNumber));
					//Console.WriteLine(ex3.StackTrace);
					connectionEnded = true;
				}
				//than block on another read operation
			}
			if(AlreadyDisconnected) this.Disconnect();
		}

		/// <summary>
		/// used to eqnueue packet by receiver thread and by GameRoomThread if packet is wrong
		/// </summary>
		/// <param name="packet"></param>
		public void EnqueuePacket(GamePacket packet) {
			lock (queueLock) {
				if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- equeuing packet: " + packet.OperationType + ", queue count = " + QueueCount);
				receivedPackets.Enqueue(packet);
				messageReceivedEvent.Set();
				if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- enqueued: " + packet.OperationType + ", queue count = " + QueueCount);
			}
		}

		/// <summary>
		/// Used to receive <see cref="GamePacket"/>s
		/// </summary>
		/// <returns>received (deserialized) GamePacket</returns>
		private GamePacket Receive() {
			if (connectionEnded) throw new ConnectionEndedException("Trying to receive when connection is closed", PlayerNumber);
			lock (receiveLock) {
				int ridn = rid.Next();
				if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- rcv start " + ridn);
				GamePacket receivedPacket = (GamePacket)serializer.Deserialize(netStream);
				if (fullDebug) tcpConnectionLogger(PlayerNumber + "-- rcv got: " + receivedPacket.OperationType + " " + ridn);
				return receivedPacket;
			}
		}

		#region connection events
		protected virtual void OnGameAbandoned(EventArgs e) {
			EventHandler handler = GameAbandoned;
			if (handler != null) {
				handler(this, e);
			}
		}

		protected virtual void OnConnectionEnded(GameEventArgs e) {
			EventHandler<GameEventArgs> handler = ConnectionEnded;
			if (handler != null) {
				handler(this, e);
			}
		}

		protected virtual void OnSurrender(GameEventArgs e) {
			EventHandler<GameEventArgs> handler = Surrender;
			if (handler != null) {
				handler(this, e);
			}
		}

		protected virtual void OnConnectionTestReceived(EventArgs e) {
			EventHandler handler = ConnectionTestReceived;
			if (handler != null) {
				handler(this, e);
			}
		}

		protected virtual void OnGameOver(EventArgs e) {
			EventHandler handler = GameOver;
			if (handler != null) {
				handler(this, e);
			}
		}
		#endregion

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
					Thread.Sleep(connectionTestIntervalMilis);
				} catch (IOException ex) {
					if (debug) tcpConnectionLogger("Connection ended");
					//Console.WriteLine(ex.StackTrace);
					connectionEnded = true;
					break;
				} catch (SerializationException ex2) {
					if (debug) tcpConnectionLogger("Connection ended");
					//Console.WriteLine(ex2.StackTrace);
					connectionEnded = true;
					break;
				} catch (Exception ex3) {
					if (debug) tcpConnectionLogger("Connection endedd");
					OnConnectionEnded(new GameEventArgs(PlayerNumber));
					//Console.WriteLine(ex3.StackTrace);
					connectionEnded = true;
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
		public Stream NetStream { get => netStream; set => netStream = value; }
		public string RemoteIpAddress { get => remoteIpAddress; set => remoteIpAddress = value; }
		public int RemotePortNumber { get => remotePortNumber; set => remotePortNumber = value; }
		public bool RemotePlannedDisconnect { get => remotePlannedDisconnect; set => remotePlannedDisconnect = value; }
		public int PlayerNumber { get => playerNumber; set => playerNumber = value; }

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
			NetStream.Dispose();
			TcpClient.Dispose();

			if (connectionTester != null) connectionTester.Join();
			if (receiver.ManagedThreadId != Thread.CurrentThread.ManagedThreadId) receiver.Join();
			AlreadyDisconnected = true;
		}
		#endregion

	}

}
