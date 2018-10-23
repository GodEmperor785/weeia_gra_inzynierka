﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using MySql.Data.MySqlClient;
using GAME_connection;
using System.Globalization;

namespace GAME_Server {
	internal class Program {
		private static int port = TcpConnection.DEFAULT_PORT;
		private static string ip = "127.0.0.1";

		//database specific fields and properties
		private static IGameDataBase gameDataBase;
		private static List<Ship> allShips;
		private static List<Faction> allFactions;
		private static BaseModifiers baseModifiers;

		//http://www.entityframeworktutorial.net/code-first/database-initialization-strategy-in-code-first.aspx
		//https://dev.mysql.com/doc/connector-net/en/connector-net-entityframework60.html
		//https://stackoverflow.com/questions/50631210/mysql-with-entity-framework-6
		//https://docs.microsoft.com/en-us/aspnet/mvc/overview/getting-started/getting-started-with-ef-using-mvc/creating-an-entity-framework-data-model-for-an-asp-net-mvc-application
		//https://stackoverflow.com/questions/21115776/setting-maxlength-for-all-strings-in-entity-framework-code-first
		//http://www.entityframeworktutorial.net/Querying-with-EDM.aspx
		//sciagnij mysql connector i zainstaluj, potem dodaj referencje
		//nuget package manager -> browse -> MySQL i dodaj MySQL.Data.Entity (zwykly MySQL.Data powinien byc dodany wczesniej przy instalacji connectora i dodaniu referencji)
		//6.9.12 mysql dziala

		//https://docs.microsoft.com/en-us/aspnet/web-forms/overview/getting-started/getting-started-with-aspnet-45-web-forms/checkout-and-payment-with-paypal
		//wymagane jest ssl i/lub tls 1.2 bo inaczej paypal moze odrzucic, wymagane sa certyfikaty, informacje sa tylko o aplikacjach webowych w przegladarce, jakies dane przekazywane przez sesje - trzebaby robic to inaczej
		//w sumie kilkaset linii kodu - w tym ponad 300 na sama klase z tutoriala (a sama klasa nie wystarczy)

		// - historia rozgrywek, kto z kim i jakie floty
		// - wirtualna waluta i kupowanie kart, przypisywanie kart do gracza, player w wersji DB, many-to-many
		// - turnieje po okolo 8 graczy o duze nagrody
		// - apka windows forms dla admina

		internal static IGameDataBase GameDataBase { get => gameDataBase; }
		public static List<Ship> AllShips { get => allShips; }
		public static List<Faction> AllFactions { get => allFactions; }
		public static BaseModifiers BaseModifiers { get => baseModifiers; }

		//thread management specific fields and properties
		private static List<Thread> userThreads = new List<Thread>();

		static void Main(string[] args) {
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");	//to change exception language to english
			InitilizeGameDataFromDB();

			IPAddress ipAddress = IPAddress.Parse(ip);
			TcpListener listener = new TcpListener(ipAddress, port);
			listener.Start();
			Console.WriteLine("Server listening on: " + ip + ":" + port);

			while (true) {
				Console.WriteLine("Server is waiting for client...");
				TcpClient client = listener.AcceptTcpClient();
				TcpConnection gameClient = new TcpConnection(client, false);
				Console.WriteLine("Client connected - ip: " + gameClient.RemoteIpAddress + " port: " + gameClient.RemotePortNumber);

				Thread t = new Thread(new ParameterizedThreadStart(Test));
				//UserThread userThread = new UserThread(gameClient);
				//Thread t = new Thread(new ThreadStart(userThread.RunUserThread));
				userThreads.Add(t);
				t.Start(gameClient);
			}
		}

		/// <summary>
		/// Reads basic game data from DB into memory. Does not read player and fleet data, these should be read by user threads
		/// </summary>
		private static void InitilizeGameDataFromDB() {
			/*gameDataBase = new InMemoryGameDataBase();
			baseModifiers = GameDataBase.GetBaseModifiers();
			allFactions = GameDataBase.GetAllFactions();
			allShips = GameDataBase.GetAllShips();*/
			using (GameDBContext dbContext = new GameDBContext()) {
				dbContext.Database.Initialize(true);

				var f1 = new Faction(1, "test");
				DbWeapon w1 = new DbWeapon("w1", f1, 10.0, 15, WeaponType.KINETIC, 1.5, 1.4, 12.0, 1);
				DbWeapon w2 = new DbWeapon("w2", f1, 12.0, 15, WeaponType.LASER, 2.6, 5.4, 88.0, 2);
				DbWeapon w3 = new DbWeapon("w3", f1, 10.0, 17, WeaponType.KINETIC, 1.0, 1.4, 55.0, 3);
				DbDefenceSystem d1 = new DbDefenceSystem("d1", f1, 5.0, DefenceSystemType.SHIELD, 2.0, 2.0, 1.3, 1);
				DbDefenceSystem d2 = new DbDefenceSystem("s2", f1, 3.0, DefenceSystemType.INTEGRITY_FIELD, 1.2, 1.3, 1.5, 2);
				List<DbWeapon> weapons1 = new List<DbWeapon>();
				weapons1.Add(w1);
				weapons1.Add(w2);
				weapons1.Add(w3);
				List<DbDefenceSystem> defs = new List<DbDefenceSystem>();
				defs.Add(d1);
				defs.Add(d2);
				DbShip s1 = new DbShip(1, "s1", f1, 10, 10.0, 1000.0, weapons1, defs, 5.0, 54.0, 2000);
				dbContext.Ships.Add(s1);
				//dbContext.Factions.Add(f1);
				dbContext.SaveChanges();

				Thread.Sleep(2000);
				var query = from ships in dbContext.Ships
							where ships.Id == 1
							select ships;
				var selectedShip = query.First<DbShip>();
				Ship properShip = selectedShip.ToShip();
				Console.WriteLine(properShip.Name);
			}
		}

		/// <summary>
		/// performs deep cloning of serializable object. If object is not serializable throws <see cref="ArgumentException"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectToClone"></param>
		/// <returns></returns>
		internal static T CloneObject<T>(T objectToClone) {
			if (!typeof(T).IsSerializable) throw new ArgumentException("Type of object must be serializable");
			else if (objectToClone == null) return default(T);
			using (var tempStream = new MemoryStream()) {
				var serializer = new BinaryFormatter();
				serializer.Serialize(tempStream, objectToClone);
				tempStream.Position = 0;
				return (T)serializer.Deserialize(tempStream);
			}
		}

		private static void Test(object clientObj) {
			try {
				TcpConnection client = (TcpConnection)clientObj;

				Console.WriteLine("Trying to receive...");
				GamePacket packet = client.GetReceivedPacket();
				Console.WriteLine("Received packet: " + packet.OperationType);

				Console.WriteLine("Sleeping some time...");
				Thread.Sleep(3000);

				Console.WriteLine("Trying to send...");
				string msg = "server test msg";
				client.Send(new GamePacket(OperationType.LOGIN, msg));

				Console.WriteLine("Trying to receive with timeout...");
				try {
					client.GetReceivedPacket(2000, 1);
				} catch (ReceiveTimeoutException e) {
					Console.WriteLine("Failed to receive with timeout. Disconnected player number: " + e.PlayerNumber);
				}

				Console.WriteLine("Trying to receive complex packet...");
				packet = client.GetReceivedPacket();
				Fleet fleet = (Fleet)packet.Packet;
				Console.WriteLine("there are " + fleet.Ships.Count + " ships in the received fleet");

				Fleet clonedFleet = CloneObject(fleet);
				Console.WriteLine("there are " + fleet.Ships.Count + " ships in the CLONED fleet");

				Console.WriteLine("Waiting for client to send DISCONNECT...");
				packet = client.GetReceivedPacket();
				if (packet.OperationType == OperationType.DISCONNECT) {
					Console.WriteLine("received disconnect");
					client.Disconnect();
				}
				else Console.WriteLine("not ok");

				Console.WriteLine("All OK! Closing...");
				//Thread.Sleep(1000);
				//client.Dispose();
			} catch(ConnectionEndedException ex) {
				Console.WriteLine("Exception: " + ex.Message + "Exception type: " + ex.ToString());
			}
			Console.WriteLine("Test end");
		}

	}

	#region User Thread
	//-------------------------------------------
	//---------USER THREAD-----------------------
	//-------------------------------------------
	internal class UserThread {
		private TcpConnection client;
		private Player user;

		internal UserThread(TcpConnection client) {
			this.client = client;
		}


		internal void RunUserThread() {

		}

	}
	#endregion

	#region Game Room Thread
	//-------------------------------------------
	//---------GAME ROOM THREAD------------------
	//-------------------------------------------
	internal class GameRoomThread {
		private Player player1;     //host
		private Player player2;

		private TcpConnection player1Conn;	//host
		private TcpConnection player2Conn;

		internal GameRoomThread(TcpConnection hostConnection, Player host) {
			player1Conn = hostConnection;
			player1 = host;
		}

		internal void RunGameThread() {

		}

	}
	#endregion

}
