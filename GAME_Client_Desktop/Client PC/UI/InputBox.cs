﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client_PC.UI
{
    class InputBox : GuiElement, IClickable, IHasText
    {
        private string text;
        public string Text
        {
            get { return text;}
            set { text = value; }
        }
        public Vector2 TextPosition { get; set; }
        public SpriteFont Font { get; set; }
        public bool Active { get; set; }
        public bool ActiveChangeable { get; set; }
        public object Parent { get; set; }
        public int TextLimit { get; set; }
        public bool TextWrappable { get; set; }

        public Rectangle GetBoundary()
        {
            return Boundary;
        }
        public delegate void ElementClicked();
        public event ElementClicked clickEvent;

        public InputBox(Point origin, int width, int height, GraphicsDevice device, GUI gui, SpriteFont font, bool wrapable) : base(origin, width, height, device, gui)
        {
            Font = font;
            ActiveChangeable = true;
            text = "";
            TextWrappable = wrapable;
        }
        public override void Update()
        {
            Vector2 z = Font.MeasureString(text);
            TextPosition = new Vector2(((Origin.X + Width / 2.0f)) - z.X / 2.0f, (Origin.Y + Height / 2.0f) - z.Y / 2.0f);
        }
        public void OnClick()
        {
            Game1.self.FocusedElement = this;
        }
        public override void Draw(SpriteBatch spriteBatch)
        {

            spriteBatch.Draw(Util.CreateTextureHollow(Device, Width, Height, pixel => Color.Black), Boundary, Color.White);
            if(!String.IsNullOrEmpty(text))
                spriteBatch.DrawString(Font, Text, TextPosition, Color.Black);
        }
    }
}