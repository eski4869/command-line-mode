using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using EntityComponent;
using JumpKing;
using JumpKing.API;
using JumpKing.BodyCompBehaviours;
using JumpKing.GameManager;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.PauseMenu.BT.Actions;
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
        private const string SettingsFileName = "eski4869.CommandLineMode.Settings.xml";

        private static CommandLineBehaviour _registeredBehaviour;
        private static CommandLinePreferences _preferences;
        private static string _settingsPath;
        private static bool _settingsDirty;
        private static bool _processExitRegistered;

        [OnLevelStart]
        public static void OnLevelStart()
        {
            EnsurePreferencesLoaded();
            CommandLineOverlay.EnsureAdded();

            if (!_preferences.IsEnabled)
            {
                UnregisterCommandLineBehaviour();
                return;
            }

            RegisterCommandLineBehaviour();
        }

        private static void RegisterCommandLineBehaviour()
        {
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

        private static void UnregisterCommandLineBehaviour()
        {
            if (_registeredBehaviour == null)
            {
                return;
            }

            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player != null)
            {
                try
                {
                    player.m_body.RemoveBehaviour(_registeredBehaviour);
                }
                catch
                {
                }
            }

            _registeredBehaviour = null;
            CommandLineState.Close();
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            SaveSettingsIfDirty();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            SaveSettingsIfDirty();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static CommandLineToggle CommandLineMenu(object factory, GuiFormat format)
        {
            return new CommandLineToggle();
        }

        public static bool IsCommandLineEnabled()
        {
            EnsurePreferencesLoaded();
            return _preferences.IsEnabled;
        }

        public static void SetCommandLineEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.IsEnabled == isEnabled)
            {
                return;
            }

            _preferences.IsEnabled = isEnabled;
            _settingsDirty = true;

            if (isEnabled)
            {
                RegisterCommandLineBehaviour();
            }
            else
            {
                UnregisterCommandLineBehaviour();
            }
        }

        private static void EnsurePreferencesLoaded()
        {
            if (_preferences != null)
            {
                RegisterProcessExit();
                return;
            }

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _settingsPath = Path.Combine(assemblyDir, SettingsFileName);

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var serializer = new XmlSerializer(typeof(CommandLinePreferences));

                    using (var stream = File.OpenRead(_settingsPath))
                    {
                        _preferences = (CommandLinePreferences)serializer.Deserialize(stream);
                    }
                }
            }
            catch
            {
            }

            if (_preferences == null)
            {
                _preferences = new CommandLinePreferences();
                _settingsDirty = true;
            }

            RegisterProcessExit();
        }

        private static void RegisterProcessExit()
        {
            if (_processExitRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitRegistered = true;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            SaveSettingsIfDirty();
        }

        private static void SaveSettingsIfDirty()
        {
            if (!_settingsDirty || _preferences == null)
            {
                return;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(CommandLinePreferences));

                using (var stream = File.Create(_settingsPath))
                {
                    serializer.Serialize(stream, _preferences);
                }

                _settingsDirty = false;
            }
            catch
            {
            }
        }
    }

    public class CommandLineToggle : ITextToggle
    {
        public CommandLineToggle() : base(ModEntry.IsCommandLineEnabled())
        {
        }

        protected override string GetName()
        {
            return "Command-line Mode";
        }

        protected override void OnToggle()
        {
            ModEntry.SetCommandLineEnabled(toggle);
        }
    }

    public class CommandLinePreferences
    {
        public bool IsEnabled { get; set; } = true;
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
            if (!ModEntry.IsCommandLineEnabled() || !CommandLineState.IsActive)
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
