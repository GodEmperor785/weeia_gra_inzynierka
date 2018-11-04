﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client_PC.UI;
using Client_PC.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client_PC.Scenes
{
    class LoginMenu : Menu
    {
        private Grid grid;


        public void Initialize(ContentManager Content)
        {
            Gui = new GUI(Content);
            Label labelLogin = new Label(new Point(0,0),100,45,Game1.self.GraphicsDevice,Gui,Gui.mediumFont,true)
            {
                Text = "Login"
            };
            Label labelPassword = new Label(new Point(0, 0), 100, 45, Game1.self.GraphicsDevice, Gui, Gui.mediumFont, true)
            {
                Text = "Password"
            };
            InputBox inputLogin = new InputBox(new Point(0,0),100,45,Game1.self.GraphicsDevice,Gui,Gui.mediumFont,false );
            inputLogin.TextLimit = 30;
            InputBox inputPassword = new InputBox(new Point(0, 0), 100, 45, Game1.self.GraphicsDevice, Gui, Gui.mediumFont, false);
            inputPassword.TextLimit = 30;
            Button loginButton = new Button(new Point(0,0),100,45,Game1.self.GraphicsDevice,Gui,Gui.mediumFont,true)
            {
                Text = "Log in"
            };
            Button registerButton = new Button(new Point(0, 0), 100, 45, Game1.self.GraphicsDevice, Gui, Gui.mediumFont, true)
            {
                Text = "Register"
            };
            Button exitButton = new Button(new Point(0, 0), 100, 45, Game1.self.GraphicsDevice, Gui, Gui.mediumFont, true)
            {
                Text = "Exit"
            };
            Clickable.Add(inputLogin);
            Clickable.Add(inputPassword);
            Clickable.Add(loginButton);
            Clickable.Add(registerButton);
            Clickable.Add(exitButton);
            grid = new Grid();
            Button refresh = new Button(new Point(0, 0), 50, 50, Game1.self.GraphicsDevice, Gui, Gui.mediumFont, true);
            refresh.clickEvent += RefResh;
            Clickable.Add(refresh);
            grid.AddChild(labelLogin,0,0);
            grid.AddChild(inputLogin,0,1,2);
            grid.AddChild(labelPassword,1,0);
            grid.AddChild(inputPassword,1,1,2);
            grid.AddChild(loginButton,2,0);
            grid.AddChild(registerButton,2,1);
            grid.AddChild(exitButton,2,2);
            grid.AddChild(refresh,3,0);
            loginButton.clickEvent += LoginClick;
            exitButton.clickEvent += ExitClick;
            registerButton.clickEvent += RegisterClick;
            grid.ResizeChildren();
        }

        public void RefResh()
        {
            Game1.self.Wallpaper = Utils.CreateTexture(Game1.self.GraphicsDevice, Game1.self.graphics.PreferredBackBufferWidth, Game1.self.graphics.PreferredBackBufferHeight);
        }
        public override void UpdateGrid()
        {
            grid.Origin = new Point((int)(Game1.self.GraphicsDevice.Viewport.Bounds.Width / 2.0f - grid.Width / 2.0f), (int)(Game1.self.GraphicsDevice.Viewport.Bounds.Height / 2.0f - grid.Height / 2.0f));
            grid.UpdateP();
        }
        public void Draw(GameTime gameTime)
        {
           
            grid.Draw(Game1.self.spriteBatch);
        }
        public void ExitClick()
        {
            Game1.self.Exit();
        }

        public void LoginClick()
        { 
            Game1.self.state = Game1.State.MainMenu;
        }

        public void RegisterClick()
        {
            Game1.self.state = Game1.State.RegisterMenu;
        }

        
    }
}
