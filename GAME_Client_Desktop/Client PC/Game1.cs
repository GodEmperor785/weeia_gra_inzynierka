﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml.Serialization;
using Client_PC.Scenes;
using Client_PC.UI;
using Client_PC.Utilities;
using GAME_connection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;
using Button = Client_PC.UI.Button;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using GameWindow = Client_PC.Scenes.GameWindow;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Label = Client_PC.UI.Label;
using MainMenu = Client_PC.Scenes.MainMenu;
using Menu = Client_PC.Scenes.Menu;

using PayPal.Api;
using TexturePackerLoader;


namespace Client_PC
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game1 : Game
    {
        public List<Ship> CardTypes;

        public enum State
        {
            LoginMenu,
            MainMenu,
            OptionsMenu,
            GameWindow,
            DeckMenu,
            RegisterMenu,
            ShopMenu,
            FleetMenu,
            CardsMenu
        }

        public class ShipAndSkin
        {
            public string ship;
            public Texture2D skin;
        }

        public class SkinAndPath
        {
            public Texture2D skin;
            public string path;
        }

        public string[] wallpapers = new string[]{
            "v1.png","v2.png","v3.png","v4.png","v5.png","v6.png"
        };
        public List<Texture2D> walls = new List<Texture2D>();
        public static Game1 self;
        public State state = State.LoginMenu;
        public GraphicsDeviceManager graphics;
        public SpriteBatch spriteBatch;
        GraphicsDevice gd;
        private MainMenu mainMenu;
        private SettingsMenu settingsMenu;
        private LoginMenu loginMenu;
        private RegisterMenu registerMenu;
        private DeckMenu deckMenu;
        private GameWindow gameWindow;
        private ShopMenu shopMenu;
        private FleetMenu fleetMenu;
        private CardsMenu cardsMenu;
        public float DeltaSeconds;
        public bool AbleToClick;
        internal Tooltip tooltipToDraw;
        internal Popup popupToDraw;
        public Config conf;
        internal object graphicsDevice;
        internal IClickable FocusedElement;
        internal Texture2D Wallpaper;
        public Player player;
        public List<Fleet> Decks { get; set; }
        public List<Ship> OwnedShips { get; set; }
        public Effect Darker;
        List<Menu> menus = new List<Menu>();
        public BaseModifiers Modifiers;
        public List<ShipAndSkin> ShipsSkins = new List<ShipAndSkin>();
        public List<SkinAndPath> SkinsPaths = new List<SkinAndPath>();
        public List<ShipAndSkin> EnemyShipsSkins = new List<ShipAndSkin>();
        public TcpConnection Connection;
        public string ServerIp = "212.191.92.88";
        private bool test = true; // false if dont connect with server
        public bool ReadyToPlay;
        private Cards config;
        private string id;
        private PayPal.Api.Payment payment;
        public SpriteSheet sheet, hit4sheet;
        public SpriteRender renderer;
        public List<Animation> animations = new List<Animation>();
        public Payments payments = new Payments()
        {
            listOfPayments = new List<GamePayment>()
        };
        public APIContext apiContext;
        private int approved = 0;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            setUpConnection();
        }
        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            SetUpPayPal();
            
            self = this;
            LoadConfig();
            LoadWallpapers();
            Wallpaper = walls[new Random().Next(6)];
            //Wallpaper = Utils.CreateTexture(GraphicsDevice, graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
            //  gd = GraphicsDevice;
            var spriteSheetLoader = new SpriteSheetLoader(Content, GraphicsDevice);
            sheet = spriteSheetLoader.Load("SpriteSheet.png");
            hit4sheet = spriteSheetLoader.Load("hit4.png");
            //sheet
            loginMenu = new LoginMenu();
            loginMenu.Initialize(Content);
            registerMenu = new RegisterMenu();
            registerMenu.Initialize(Content);
            menus.Add(loginMenu);
            menus.Add(registerMenu);
            base.Initialize();
            
        }

        public void SetUpPayPal()
        {
            var config = ConfigManager.Instance.GetProperties();

            var accessToken = new OAuthTokenCredential(config).GetAccessToken();

            apiContext = new APIContext(accessToken);

        }

        public void Checking()
        {
            while (true)
            {
                foreach (var gamePayment in payments.listOfPayments)
                {
                    var id = gamePayment.Id;
                    var g = Payment.Get(apiContext, id);
                    if (g.payer != null)
                    {
                        if (g.state == "approved")
                        {
                            //gdy transakcja zostala wykonana przez paypala
                            //GamePacket packet = new GamePacket(OperationType.GET_CREDITS,gamePayment.Name);
                            //Connection.Send(packet);
                        }
                        else
                        {
                            //gdy transakcja zostaje zaakceptowana przez platnika
                            var paymentExecution = new PaymentExecution();
                            paymentExecution.payer_id = g.payer.payer_info.payer_id;
                            payment.Execute(apiContext, paymentExecution);
                        }
                        
                    }
                    
                }
                
                //PaymentExecution z = new PaymentExecution();
                //payment.Execute(apiContext,)
                Thread.Sleep(10000);
            }
        }

        public void LoadWallpapers()
        {
            foreach (var wallpaper in wallpapers)
            {
                using (FileStream fileStream = new FileStream("Content/"+wallpaper, FileMode.Open))
                {
                    Texture2D wall = Texture2D.FromStream(Game1.self.GraphicsDevice, fileStream);
                    walls.Add(wall);
                    fileStream.Dispose();
                }
            }
        }
        public void LoginInitialize()
        {
            mainMenu = new MainMenu();
            settingsMenu = new SettingsMenu();
            settingsMenu.Initialize(Content);
            mainMenu.Initialize(Content);
            deckMenu = new DeckMenu();
            deckMenu.Initialize(Content);
            gameWindow = new GameWindow();
            gameWindow.Initialize(Content);
            shopMenu = new ShopMenu();
            shopMenu.Initialize(Content);
            fleetMenu = new FleetMenu();
            fleetMenu.Initialize(Content);
            cardsMenu = new CardsMenu();
            cardsMenu.Initialize(Content);
            menus.Add(mainMenu);
            menus.Add(settingsMenu);
            menus.Add(deckMenu);
            menus.Add(gameWindow);
            menus.Add(shopMenu);
            menus.Add(fleetMenu);
            menus.Add(cardsMenu);

            settingsMenu.SetMenus(menus);
        }
        public void setUpConnection()
        {
            if (test)
            {
                try
                {
                    int port = TcpConnection.DEFAULT_PORT_CLIENT;
                    TcpClient client = new TcpClient(ServerIp, port);
                    Connection = new TcpConnection(client, true, Nothing, false, true);
                }
                catch (Exception e)
                {
                    
                }
            }
        }

        public void Nothing(String c)
        {

        }
        public void Quit()
        {
            Connection.SendDisconnect();
            this.Exit();
        }

        private void LoadAllCards()
        {
            CardTypes = new List<Ship>();
            GamePacket packet = new GamePacket(OperationType.GET_SHIP_TEMPLATES, null);
            Connection.Send(packet);
            packet = Connection.GetReceivedPacket();
            if (packet.OperationType == OperationType.GET_SHIP_TEMPLATES)
            { //TODO make it when server is working with it
                CardTypes = (List<Ship>) packet.Packet;
            }

        }

        public void LoadCardTextures()
        {
            
            LoadAllCards();
            XmlSerializer serializer = new XmlSerializer(typeof(Cards));
            try
            {
                using (FileStream fs = new FileStream("Config_Cards", FileMode.Open))
                {

                }

            }
            catch
            {
                TextWriter writer = new StreamWriter("Config_Cards");
                config = new Cards();
                config.listOfCards = new List<CardConfig>();
                CardTypes.ForEach(p =>
                {
                    CardConfig c = new CardConfig();
                    c.Name = p.Name;
                    c.SkinPath = String.Empty;
                    config.listOfCards.Add(c);
                });
                XmlSerializer xml = new XmlSerializer(typeof(Cards));
                xml.Serialize(writer, config);
                writer.Close();
            }
            using (Stream reader = new FileStream("Config_Cards", FileMode.Open))
            {
                config = (Cards)serializer.Deserialize(reader);
            }
            
            config.listOfCards.ForEach(p =>
            {
                CardTypes.Where(a=> a.Name == p.Name).ToList().ForEach(z =>
                {
                    try
                    {
                        Texture2D skin = File.Exists(p.SkinPath) ? loadTexture2D(p.SkinPath) : null;
                        ShipsSkins.Add(new ShipAndSkin()
                        {
                            ship = z.Name,
                            skin = skin

                        });
                        SkinsPaths.Add(new SkinAndPath()
                        {
                            skin = skin,
                            path = p.SkinPath
                        });
                    }
                    catch { }
                });
            });
        }

        public void SetTextureToShip(string path, string shipName)
        {
            string newPath = AppDomain.CurrentDomain.BaseDirectory + Content.RootDirectory + "\\Skins\\" + shipName+".png";
            File.Copy(path, newPath,true);
            ShipAndSkin s = ShipsSkins.Single(p => p.ship == shipName);
            s.skin = loadTexture2D(newPath);
            TextWriter writer = new StreamWriter("Config_Cards");
            config.listOfCards.Single(p => p.Name == shipName).SkinPath = newPath;

            XmlSerializer xml = new XmlSerializer(typeof(Cards));
            xml.Serialize(writer, config);
            writer.Close();
        }
        private Texture2D loadTexture2D(string path)
        {
            Texture2D text = null;
            try
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open))
                {
                    text = Texture2D.FromStream(Game1.self.GraphicsDevice, fileStream);
                    fileStream.Dispose();
                }
            }
            catch { }
            return text;
        }
        private void LoadConfig()
        {
            XmlSerializer serializer =
                new XmlSerializer(typeof(Config));
            try
            {
                using (FileStream fs = new FileStream("Config", FileMode.Open))
                {

                }

            }
            catch
            {
                TextWriter writer = new StreamWriter("Config");
                XmlSerializer xml = new XmlSerializer(typeof(Config));
                xml.Serialize(writer, Config.Default());
                writer.Close();
            }



            using (Stream reader = new FileStream("Config", FileMode.Open))
            {
                conf = (Config)serializer.Deserialize(reader);
            }

            UseConfig();
        }
        private void UseConfig()
        {
            #region Resolution

            int height = 0;
            int width = 0;
            if (conf.Resolution.Equals(Constants.hd))
            {
                height = 720;
                width = 1080;
            }
            if (conf.Resolution.Equals(Constants.fullhd))
            {
                height = 1080;
                width = 1920;
            }

            Game1.self.graphics.PreferredBackBufferHeight = height;
            Game1.self.graphics.PreferredBackBufferWidth = width;
            Game1.self.graphics.ApplyChanges();
            #endregion
        }
        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            Darker = Content.Load<Effect>("Shaders/GrayScaleShader");
            renderer = new SpriteRender(spriteBatch);
            Form MyGameForm = (Form)Form.FromHandle(Window.Handle);
            MyGameForm.Closing += ClosingFunction;
            // TODO: use this.Content to load your game content here
        }
        public void ClosingFunction(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Connection?.Disconnect();

        }

        public void StartGame()
        {
            gameWindow.Start();
        }

        public void SetMoney(int amount)
        {
            shopMenu.SetMoney(amount);
        }

        public void SetSettings()
        {
            settingsMenu.FillGridWithCardTypes(CardTypes);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
            if (ReadyToPlay)
            {
                ReadyToPlay = false;
                state = State.FleetMenu;
            }
            switch (state)
            {
                case State.MainMenu:
                    mainMenu.Update(gameTime);
                    break;
                case State.OptionsMenu:
                    settingsMenu.Update(gameTime);
                    break;
                case State.LoginMenu:
                    loginMenu.Update(gameTime);
                    break;
                case State.RegisterMenu:
                    registerMenu.Update(gameTime);
                    break;
                case State.DeckMenu:
                    deckMenu.Update(gameTime);
                    break;
                case State.GameWindow:
                    gameWindow.Update(gameTime);
                    break;
                case State.ShopMenu:
                    shopMenu.Update(gameTime);
                    break;
                case State.FleetMenu:
                    fleetMenu.Update(gameTime);
                    break;
                case State.CardsMenu:
                    cardsMenu.Update(gameTime);
                    break;
            }

            var toRemove = new List<Animation>();
            animations.ForEach(p =>
            {
                if (p.Update())
                {
                    toRemove.Add(p);
                }
            });
            toRemove.ForEach(p=> animations.Remove(p));
            base.Update(gameTime);
        }

        public void CleanDeck()
        {
            deckMenu.Clean();
        }

        public void CleanCards()
        {
            cardsMenu.Clean();
        }

        public void SetDecks(List<Fleet> fleets)
        {
            deckMenu.LoadDecksAndShips(fleets, OwnedShips);
        }
        public void CleanLogin()
        {
            loginMenu.Clean();
        }

        public void UpdatePlayer()
        {
            mainMenu.UpdatePlayer();
        }
        public void SetShop(List<LootBox> loots)
        {
            shopMenu.Reinitialize(loots);
        }

        public void SetFleetMenu(Fleet fleet)
        {
            fleetMenu.ReDo();
            fleetMenu.setFleet(fleet);
            fleetMenu.Fill();
        }
        public void CleanRegister()
        {
            registerMenu.Clean();
        }

        public void UpdateHistory()
        {
            GamePacket packet = new GamePacket(OperationType.GET_PLAYER_STATS,Game1.self.player);
            Connection.Send(packet);
            packet = Connection.GetReceivedPacket();
            if (packet.OperationType == OperationType.GET_PLAYER_STATS)
            {
                mainMenu.FillHistory(( List<GameHistory>)packet.Packet);
            }
            
        }
        private void WallpaperChange()
        {
            Random rndRandom = new Random();
            int width = Wallpaper.Width;
            double minAddition = 1;
            double maxAddition = 1;
            int colorRange = 5;
            int startMin = 0;
            int startMax = 255;
            Color[] data = new Color[Wallpaper.Width * Wallpaper.Height];
            Wallpaper.GetData(data);
            for (int i = 0; i < 10000; i++)
            {
                int z = rndRandom.Next(0, data.Length - 1);
                ChangePixel(z, width, data, rndRandom, minAddition, colorRange, maxAddition, startMin, startMax);




            }
            Wallpaper.SetData(data);
            
        }

        private void ChangePixel(int z, int width, Color[] data, Random rndRandom, double minAddition, int colorRange,
            double maxAddition, int startMin, int startMax)
        {
            Color empty = new Color();
            if (z % width > 0 && data[z - 1] != empty)
            {
                if (z - width > 0 && data[z - width] != empty) // inside
                {
                    if (z - 2 * width > 0 && data[z - 2 * width] != empty && (z - 2) % width > 0) // further rows inside
                    {
                        Color cl = new Color(
                                rndRandom.Next(
                                    (int) Math.Round((data[z - 1].R + data[z - width].R + data[z - 2].R +
                                                      data[z - 2 * width].R + minAddition) / 4.0f - colorRange),
                                    (int) Math.Round((data[z - 1].R + data[z - width].R + data[z - 2].R +
                                                      data[z - 2 * width].R + maxAddition) / 4.0f + colorRange)),
                                rndRandom.Next(
                                    (int) Math.Round((data[z - 1].G + data[z - width].G + data[z - 2].G +
                                                      data[z - 2 * width].G + minAddition) / 4.0f - colorRange),
                                    (int) Math.Round((data[z - 1].G + data[z - width].G + data[z - 2].G +
                                                      data[z - 2 * width].G + maxAddition) / 4.0f + colorRange)),
                                rndRandom.Next(
                                    (int) Math.Round((data[z - 1].B + data[z - width].B + data[z - 2].B +
                                                      data[z - 2 * width].B + minAddition) / 4.0f - colorRange),
                                    (int) Math.Round((data[z - 1].B + data[z - width].B + data[z - 2].B +
                                                      data[z - 2 * width].B + maxAddition) / 4.0f + colorRange)))
                            ;
                        data[z] = cl;
                    }
                    else // first row inside
                    {
                        Color cl = new Color(
                                rndRandom.Next((int) Math.Floor((data[z - 1].R + data[z - width].R) / 2.0f - colorRange),
                                    (int) Math.Ceiling((data[z - 1].R + data[z - width].R) / 2.0f + colorRange)),
                                rndRandom.Next((int) Math.Floor((data[z - 1].G + data[z - width].G) / 2.0f - colorRange),
                                    (int) Math.Ceiling((data[z - 1].G + data[z - width].G) / 2.0f + colorRange)),
                                rndRandom.Next((int) Math.Floor((data[z - 1].B + data[z - width].B) / 2.0f - colorRange),
                                    (int) Math.Ceiling((data[z - 1].B + data[z - width].B) / 2.0f + colorRange)))
                            ;
                        data[z] = cl;
                    }
                }
                else // top edge
                {
                    Color cl = new Color(
                        rndRandom.Next(data[z - 1].R - colorRange, data[z - 1].R + colorRange),
                        rndRandom.Next(data[z - 1].G - colorRange, data[z - 1].G + colorRange),
                        rndRandom.Next(data[z - 1].B - colorRange, data[z - 1].B + colorRange)
                    );
                    data[z] = cl;
                }
            }
            else if (z - width >= 0 && data[z - width] != empty) // left edge
            {
                Color cl = new Color(
                    rndRandom.Next((data[z - width].R) - colorRange, (data[z - width].R) + colorRange),
                    rndRandom.Next((data[z - width].G) - colorRange, (data[z - width].G) + colorRange),
                    rndRandom.Next((data[z - width].B) - colorRange, (data[z - width].B) + colorRange));

                data[z] = cl;
            }
            else //  first pixel
            {
                Color cl = new Color(rndRandom.Next(startMin, startMax), rndRandom.Next(startMin, startMax),
                    rndRandom.Next(startMin, startMax));
                data[z] = cl;
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            Game1.self.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            //Darker.CurrentTechnique.Passes[0].Apply();
            if (popupToDraw != null)
            {
                Darker.CurrentTechnique.Passes[0].Apply();
                popupToDraw.Draw(spriteBatch);
            }
            Game1.self.spriteBatch.Draw(Game1.self.Wallpaper, new Vector2(0, 0), Color.White);
            
            switch (state)
            {
                case State.MainMenu:
                    mainMenu.Draw(gameTime);
                    break;
                case State.OptionsMenu:
                    settingsMenu.Draw(gameTime);
                    break;
                case State.LoginMenu:
                    loginMenu.Draw(gameTime);
                    break;
                case State.RegisterMenu:
                    registerMenu.Draw(gameTime);
                    break;
                case State.DeckMenu:
                    deckMenu.Draw(gameTime);
                    break;
                case State.GameWindow:
                    gameWindow.Draw(gameTime);
                    break;
                case State.ShopMenu:
                    shopMenu.Draw(gameTime);
                    break;
                case State.FleetMenu:
                    fleetMenu.Draw(gameTime);
                    break;
                case State.CardsMenu:
                    cardsMenu.Draw(gameTime);
                    break;
            }

            if (tooltipToDraw != null)
            {
                tooltipToDraw.Draw(spriteBatch);
            }
            base.Draw(gameTime);
            Game1.self.spriteBatch.End();
            if (popupToDraw != null)
            {
                Game1.self.spriteBatch.Begin();
                popupToDraw.Draw(spriteBatch);
                Game1.self.spriteBatch.End();
            }
            animations.ForEach(p=> p.Draw(spriteBatch));
        }
    }
}
