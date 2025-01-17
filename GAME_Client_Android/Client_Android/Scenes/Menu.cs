﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client_Android;
using Client_PC.UI;
using Client_PC.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Client_PC.Scenes
{
    class Menu
    {
        protected List<IClickable> Clickable;
        protected GUI Gui;
        protected bool AbleToClick = false;
        protected Popup popup;
        protected Keys[] LastPressedKeys;
        protected int MoveId;
        protected Vector2 location;
        
        private ButtonState lastState;
        public Menu()
        {
            Clickable = new List<IClickable>();
        }

        public virtual void Update(GameTime gameTime)
        {
            bool update = false;
            Game1.self.DeltaSeconds += gameTime.ElapsedGameTime.Milliseconds;
            Game1.self.AbleToClick = Game1.self.DeltaSeconds > Constants.clickDelay;

            var touches = TouchPanel.GetState();

            foreach (var touch in touches)
            {
                if (touch.State == TouchLocationState.Pressed)
                {
                    CheckTooltips(touch.Position);
                    location = touch.Position;
                }

                if (touch.State == TouchLocationState.Moved)
                {
                    CheckTooltips(touch.Position);
                }

                if (touch.State == TouchLocationState.Released)
                {
                    if(location != null)
                        CheckClickables(location);
                    Clickable.ForEach(p=> p.IsOver = false);
                    Game1.self.tooltipToDraw = null;
                }
            }




            /* TODO to be overwritten to be used in android way
            var keyboardState = Keyboard.GetState();
            update = Utils.UpdateKeyboard(keyboardState, ref LastPressedKeys);
            if (update)
            {
                int x = mouseState.X;
                int y = mouseState.Y;
                Point xy = new Point(x, y);
                Test(xy);
            }
            UpdateGrid();
            if(lastState != mouseState.LeftButton)
                CheckClickables(mouseState);
            CheckTooltips(mouseState);

            lastState = mouseState.LeftButton;


    */

            UpdateGrid();
            UpdateLast();
        }

        public virtual void DataInserted()
        {

        }
        protected virtual void SetClickables(bool active)
        {
            foreach (var clickable in Clickable)
            {
               // if (!(clickable is Card))
                clickable.Active = active;
                if(popup != null)
                    if (clickable.Parent == popup.grid || clickable.Parent == popup.layout)
                        clickable.Active = !active;
            }

        }

        public virtual void Clean()
        {

        }
        private void CheckClickables(Vector2 position)
        {
            if (Game1.self.IsActive)
            {
                int x = (int)position.X;
                int y = (int)position.Y;
                Point xy = new Point(x, y);
                IClickable button = GetClickable(xy);
                if (Game1.self.AbleToClick)
                {
                    UpdateFields();
                    Game1.self.DeltaSeconds = 0;
                    Game1.self.AbleToClick = false;

                    if (button != null)
                    {
                        UpdateClick(button);
                        UpdateButtonNotNull();
                    }
                    else
                    {
                        Game1.self.FocusedElement = null;
                        UpdateButtonNull();
                        hideKeyboard();
                    }

                    UpdateClickables();
                }
            }
        }

        private void hideKeyboard()
        {
            Game1.self.activitySelf.HideKeyboard();
        }
        public virtual void Test(Point xy)
        {
            var click1 = Clickable.FirstOrDefault(p => p.GetBoundary().Contains(xy));
            var click2 = Clickable.Where(p => p.Active == true).FirstOrDefault(p => p.GetBoundary().Contains(xy));
            var clicks = Clickable.Where(p => p.Active == true).Where(p => p.GetBoundary().Contains(xy)).ToList();
            var clicks2 = Clickable.Where(p => p.GetBoundary().Contains(xy)).ToList();
            var z = 123;
        }
        public virtual void UpdateLast()
        {

        }
        public virtual void UpdateButtonNotNull()
        {

        }

        public virtual void UpdateButtonNull()
        {

        }

        public virtual void UpdateClickables()
        {

        }
        private void CheckTooltips(Vector2 position)
        {
            
            int x = (int)position.X;
            int y = (int)position.Y;
            Point xy = new Point(x, y);
            IClickable button = GetClickable(xy);
            List<IClickable>c = new List<IClickable>();
            if ((button != null && button is Card) || (button != null && button.Active)) 
            {
                c.Add(button);
                button.IsOver = true;
            }

            
            Clickable.Except(c).ToList().ForEach(p => { p.IsOver = false; });
             
            UpdateTooltips(button, xy);
        }
        public virtual void Initialize(ContentManager Content)
        {

        }
        public void Reinitialize(ContentManager Content)
        {
            Clickable.Clear();
            Initialize(Content);
        }
        public virtual void UpdateGrid()
        {

        }

        public virtual void UpdateTooltips(IClickable button, Point xy)
        {
            foreach (var clickable in Clickable.Where(p => p.Tooltip != null).ToList())
            {
                Game1.self.tooltipToDraw = null;
            }
            if (button != null)
            {
                if (button.Tooltip != null)
                {
                    
                    Game1.self.tooltipToDraw = button.Tooltip;
                    button.Tooltip.Update(xy);
                }
            }
        }
        public virtual IClickable GetClickable(Point xy)
        {
            
            IClickable click = Clickable.Where(p=> p.Active == true).FirstOrDefault(p => p.GetBoundary().Contains(xy));
           
            return click;
        }
        public virtual void UpdateClick(IClickable button)
        {
            Game1.self.FocusedElement = button;
            button.OnClick();
        }

        public virtual void UpdateFields()
        {

        }
    }
}
