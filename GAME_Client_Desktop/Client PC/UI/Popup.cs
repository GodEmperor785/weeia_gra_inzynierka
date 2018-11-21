﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client_PC.UI
{
    class Popup : GuiElement
    {
        public Grid grid;
        private Texture2D Background;
        private bool active;
        public bool Active
        {
            get { return active; }
        }

        public Popup(Point origin,int width, int height, GraphicsDevice device, GUI gui) : base(width, height, device, gui)
        {
            Origin = origin;
        }

        public override void Update()
        {
            
        }

        public void SetToGrid()
        {
            Width = grid.Width + 20;
            Height = grid.Height + 20;
            grid.Origin = Origin + new Point(10, 10);
            SetBackground();
            grid.UpdateP();
        }

        private void SetBackground()
        {
            Background = Util.CreateTexture(Game1.self.GraphicsDevice, Width, Height, pixel => Color.White,
                new Color(200, 200, 200), new Color(70, 120, 180),9);
        }

        public override void Draw(SpriteBatch sp)
        {
            sp.Begin();
            sp.Draw(Background, Origin.ToVector2(), Color.White);
            sp.End();
            
            grid.Draw(sp);
        }

        public void SetActive(bool active)
        {
            this.active = active;
            grid.UpdateActive(active);
        }
    }
}
