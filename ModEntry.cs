using System;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.Mods;
using JumpKing.Player;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CommandLineMode
{
    [JumpKingMod("eski4869.CommandLineMode")]
    public static class ModEntry
    {
        private static CommandLineBehaviour _registeredBehaviour;

        [OnLevelStart]
        public static void OnLevelStart()
        {
            CommandLineOverlay.EnsureAdded();

            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
                return;
            }

            if (_registeredBehaviour != null)
            {
                try
                {
                    player.m_body.RemoveBehaviour(_registeredBehaviour);
                }
                catch
                {
                }
            }

            _registeredBehaviour = new CommandLineBehaviour();
            player.m_body.RegisterBehaviour(_registeredBehaviour);
        }
    }

    public class CommandLineBehaviour : IBodyCompBehaviour
    {
        private const int MinScreen = 1;
        private const int MaxScreen = 169;
        private const int ScreenHeight = 360;

        private KeyboardState _previousKeyboardState;

        public bool ExecuteBehaviour(BehaviourContext behaviourContext)
        {
            KeyboardState keyboardState = Keyboard.GetState();

            if (!CommandLineState.IsActive)
            {
                if (WasKeyPressed(keyboardState, Keys.OemSemicolon) &&
                    (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)))
                {
                    CommandLineState.Open();
                }

                _previousKeyboardState = keyboardState;
                return true;
            }

            if (WasKeyPressed(keyboardState, Keys.Escape))
            {
                CommandLineState.Close();
                _previousKeyboardState = keyboardState;
                return true;
            }

            if (WasKeyPressed(keyboardState, Keys.Back))
            {
                CommandLineState.Backspace();
                _previousKeyboardState = keyboardState;
                return true;
            }

            if (WasKeyPressed(keyboardState, Keys.Enter))
            {
                ExecuteCommand(CommandLineState.Text);
                CommandLineState.Close();
                _previousKeyboardState = keyboardState;
                return true;
            }

            for (int digit = 0; digit <= 9; digit++)
            {
                if (WasKeyPressed(keyboardState, NumberKey(digit)) ||
                    WasKeyPressed(keyboardState, NumPadKey(digit)))
                {
                    CommandLineState.AppendDigit(digit);
                }
            }

            _previousKeyboardState = keyboardState;
            return true;
        }

        private void ExecuteCommand(string commandText)
        {
            int screen;

            if (!int.TryParse(commandText, out screen))
            {
                return;
            }

            if (screen < MinScreen || screen > MaxScreen)
            {
                return;
            }

            TeleportToScreen(screen);
        }

        private void TeleportToScreen(int screen)
        {
            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null)
            {
                return;
            }

            int currentScreen = JumpKing.Camera.CurrentScreen + 1;
            float screenDelta = screen - currentScreen;
            player.m_body.Position.Y -= screenDelta * ScreenHeight;
            player.m_body.Velocity = Vector2.Zero;
            JumpKing.Camera.UpdateCamera(player.m_body.GetHitbox().Center);
        }

        private bool WasKeyPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private Keys NumberKey(int digit)
        {
            return (Keys)((int)Keys.D0 + digit);
        }

        private Keys NumPadKey(int digit)
        {
            return (Keys)((int)Keys.NumPad0 + digit);
        }
    }

    public static class CommandLineState
    {
        public static bool IsActive { get; private set; }
        public static string Text { get; private set; }

        public static void Open()
        {
            IsActive = true;
            Text = string.Empty;
        }

        public static void Close()
        {
            IsActive = false;
            Text = string.Empty;
        }

        public static void AppendDigit(int digit)
        {
            if (!IsActive || Text.Length >= 3)
            {
                return;
            }

            Text += digit.ToString();
        }

        public static void Backspace()
        {
            if (!IsActive || Text.Length == 0)
            {
                return;
            }

            Text = Text.Substring(0, Text.Length - 1);
        }
    }

    public class CommandLineOverlay : Entity
    {
        private static CommandLineOverlay _entity;

        public static void EnsureAdded()
        {
            if (EntityManager.instance == null)
            {
                return;
            }

            if (_entity != null && _entity.IsAlive)
            {
                return;
            }

            _entity = new CommandLineOverlay();
            EntityManager.instance.AddObject(_entity);
        }

        public override void Draw()
        {
            if (!CommandLineState.IsActive)
            {
                return;
            }

            SpriteFont font = GetFont();

            if (font == null)
            {
                return;
            }

            TextHelper.DrawString(
                font,
                ":" + CommandLineState.Text,
                GetDrawPosition(),
                Color.White,
                Vector2.Zero,
                true
            );
        }

        private static SpriteFont GetFont()
        {
            if (Game1.instance == null || Game1.instance.contentManager == null)
            {
                return null;
            }

            if (Game1.instance.contentManager.font.MenuFont != null)
            {
                return Game1.instance.contentManager.font.MenuFont;
            }

            return Game1.instance.contentManager.font.MenuFontSmall;
        }

        private static Vector2 GetDrawPosition()
        {
            return new Vector2(12f, 336f);
        }
    }
}
