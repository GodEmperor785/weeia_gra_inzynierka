﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client_PC.UI
{
    class GuiElement
    {
        public Rectangle Boundary => new Rectangle(Origin.X, Origin.Y, Width, Height);
        public Rectangle TextBox => new Rectangle(Origin.X + borderSize,Origin.Y+borderSize,Width - 2 * borderSize, Height - 2 * borderSize);
        private int borderSize = 7;
        public virtual int Width { get; set; }
        public virtual int Height { get; set; }
        public Point Origin { get; set; }
        protected GUI Gui { get; set; }
        protected GraphicsDevice Device { get; set; }
        public Texture2D Texture { get; set; }
        public int Id { get; set; }
        public virtual void Draw(SpriteBatch sp) { }
        
        public GuiElement()
        {
        }

        public GuiElement(Point origin, int width, int height, GraphicsDevice device, GUI gui)
        {
            Origin = origin;
            Width = width;
            Height = height;
            Device = device;
            Gui = gui;
            using (FileStream st = new FileStream("Content/Graphics/Button/ButtonTexture.png", FileMode.Open))
            {
                Texture = Texture2D.FromStream(Game1.self.GraphicsDevice, st);
            }
        }

        public GuiElement(int width, int height, GraphicsDevice device, GUI gui)
        {
            Width = width;
            Height = height;
            Device = device;
            Gui = gui;
        }

        protected String parseText(String text, SpriteFont Font)
        {
            String line = String.Empty;
            String returnString = String.Empty;
            String[] wordArray = text.Split(' ');
            int usedHeight = 0;
            foreach (String word in wordArray)
            {
                if (Font.MeasureString(line + word).Length() > TextBox.Width)
                {
                    returnString = returnString + line + '\n';
                    line = String.Empty;
                }

                int z = 0;
                if (line == String.Empty)
                {
                    z = (int)Font.MeasureString(line + word).Y;
                }
                if (!((usedHeight += z) > TextBox.Height))
                {
                    line = line + word + ' ';
                }
            }

            return returnString + line;
        }

        public virtual void Update()
        {

        }
        protected virtual void Update(string text, ref Vector2 TextPosition, SpriteFont Font)
        {
            if (text != null)
            {
                Vector2 z = Font.MeasureString(text);
                if (z.X < Width)
                {
                    TextPosition = new Vector2(((Boundary.X + Width / 2.0f)) - z.X / 2.0f,
                        (Boundary.Y + Height / 2.0f) - z.Y / 2.0f);
                }
                else
                {
                    TextPosition = new Vector2(((TextBox.X)), (TextBox.Y));
                }
            }
        }
    }
}
