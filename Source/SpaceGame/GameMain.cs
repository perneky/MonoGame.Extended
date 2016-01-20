﻿using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Animations;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Gui;
using MonoGame.Extended.Gui.Controls;
using MonoGame.Extended.Gui.Drawables;
using MonoGame.Extended.Screens;
using MonoGame.Extended.TextureAtlases;
using MonoGame.Extended.ViewportAdapters;
using SpaceGame.Entities;

namespace SpaceGame
{
    public class GameMain : Game
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly EntityManager _entityManager;
        private readonly ScreenManager _screenManager;
        private GuiManager _guiManager;

        private SpriteBatch _spriteBatch;
        private Texture2D _backgroundTexture;
        private Spaceship _player;
        private Camera2D _camera;
        private BitmapFont _font;
        private MouseState _previousMouseState;
        private ViewportAdapter _viewportAdapter;
        private MeteorFactory _meteorFactory;
        private SpriteSheetAnimationGroup _explosionAnimations;
        private BulletFactory _bulletFactory;
        private int _score;

        public GameMain()
        {
            _graphicsDeviceManager = new GraphicsDeviceManager(this);
            _screenManager = new ScreenManager(this);
            _entityManager = new EntityManager();

            Content.RootDirectory = "Content";
            Window.AllowUserResizing = true;
            IsMouseVisible = true;
        }

        //protected override void Initialize()
        //{
        //    base.Initialize();

        //    _graphicsDeviceManager.IsFullScreen = true;
        //    _graphicsDeviceManager.PreferredBackBufferWidth = GraphicsDevice.DisplayMode.Width;
        //    _graphicsDeviceManager.PreferredBackBufferHeight = GraphicsDevice.DisplayMode.Height;
        //    _graphicsDeviceManager.ApplyChanges();
        //}

        protected override void LoadContent()
        {
            _viewportAdapter = new BoxingViewportAdapter(Window, GraphicsDevice, 800, 480);
            _guiManager = new GuiManager(_viewportAdapter, GraphicsDevice);
            _font = Content.Load<BitmapFont>("Fonts/courier-new-32");

            var normal = new GuiTextureRegionDrawable(new TextureRegion2D(Content.Load<Texture2D>("Gui/button-normal")));
            var pressed = new GuiTextureRegionDrawable(new TextureRegion2D(Content.Load<Texture2D>("Gui/button-clicked")));
            var hover = new GuiTextureRegionDrawable(new TextureRegion2D(Content.Load<Texture2D>("Gui/button-hover")));
            var buttonStyle = new GuiButtonStyle(normal, pressed, hover);
            var button = new GuiButton(buttonStyle)
            {
                Position = new Vector2(400, 240)
            };
            button.Clicked += (sender, args) =>
            {
                if (_player != null)
                {
                    Explode(_player.Position, 3);
                    _player.Destroy();
                    _player = null;
                }
            };
            _guiManager.Controls.Add(button);

            var labelStyle = new GuiLabelStyle(_font);
            var label = new GuiLabel(labelStyle, "Hello")
            {
                Position = new Vector2(100, 100)
            };
            label.MouseMoved += (sender, args) => label.Text = args.Position.ToString();
            _guiManager.Controls.Add(label);
            
            _camera = new Camera2D(_viewportAdapter);
            _explosionAnimations = Content.Load<SpriteSheetAnimationGroup>("explosion-animations");

            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _backgroundTexture = Content.Load<Texture2D>("black");

            var bulletTexture = Content.Load<Texture2D>("laserBlue03");
            var bulletRegion = new TextureRegion2D(bulletTexture);
            _bulletFactory = new BulletFactory(_entityManager, bulletRegion);

            SpawnPlayer(_bulletFactory);

            _meteorFactory = new MeteorFactory(_entityManager, Content);

            for (var i = 0; i < 13; i++)
                _meteorFactory.SpawnNewMeteor(_player.Position);
        }

        private void SpawnPlayer(BulletFactory bulletFactory)
        {
            var spaceshipTexture = Content.Load<Texture2D>("playerShip1_blue");
            var spaceshipRegion = new TextureRegion2D(spaceshipTexture);
            _player = _entityManager.AddEntity(new Spaceship(spaceshipRegion, bulletFactory));
        }


        protected override void UnloadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            var deltaTime = gameTime.GetElapsedSeconds();
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            if (keyboardState.IsKeyDown(Keys.Escape))
                Exit();

            if (_player != null && !_player.IsDestroyed)
            {
                const float acceleration = 5f;

                if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
                    _player.Accelerate(acceleration);

                if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
                    _player.Accelerate(-acceleration);

                if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
                    _player.Rotation -= deltaTime*3f;

                if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
                    _player.Rotation += deltaTime*3f;

                if (keyboardState.IsKeyDown(Keys.Space) || mouseState.LeftButton == ButtonState.Pressed)
                    _player.Fire();

                if (_previousMouseState.X != mouseState.X || _previousMouseState.Y != mouseState.Y)
                    _player.LookAt(_camera.ScreenToWorld(new Vector2(mouseState.X, mouseState.Y)));

                _camera.LookAt(_player.Position + _player.Velocity * 0.2f);
            }

            _entityManager.Update(gameTime);

            CheckCollisions();

            _previousMouseState = mouseState;


            _guiManager.Update(gameTime);
            base.Update(gameTime);
        }

        private void CheckCollisions()
        {
            var meteors = _entityManager.Entities.Where(e => e is Meteor).Cast<Meteor>().ToArray();
            var lasers = _entityManager.Entities.Where(e => e is Laser).Cast<Laser>().ToArray();

            foreach (var meteor in meteors)
            {
                if (_player != null && !_player.IsDestroyed && _player.BoundingCircle.Intersects(meteor.BoundingCircle))
                {
                    Explode(meteor.Position, meteor.Size);
                    Explode(_player.Position, 3);

                    _player.Destroy();
                    _player = null;
                    meteor.Destroy();
                }

                foreach (var laser in lasers.Where(laser => meteor.Contains(laser.Position)))
                {
                    meteor.Damage(1);
                    laser.Destroy();
                    _score++;

                    Explode(laser.Position, meteor.Size);

                    if(meteor.Size >= 2)
                        _meteorFactory.SplitMeteor(meteor);
                }
            }
        }

        private void Explode(Vector2 position, float radius)
        {
            var explosion = new Explosion(_explosionAnimations, position, radius);
            _entityManager.AddEntity(explosion);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // background
            var sourceRectangle = new Rectangle(0, 0, _viewportAdapter.VirtualWidth, _viewportAdapter.VirtualHeight);
            sourceRectangle.Offset(_camera.Position);
            _spriteBatch.Begin(samplerState: SamplerState.LinearWrap, transformMatrix: _camera.GetViewMatrix());
            _spriteBatch.Draw(_backgroundTexture, _camera.Position, sourceRectangle, Color.White);
            _spriteBatch.End();

            // entities
            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.AlphaBlend, transformMatrix: _camera.GetViewMatrix());
            _entityManager.Draw(_spriteBatch);
            _spriteBatch.End();

            // hud
            _spriteBatch.Begin();
            _spriteBatch.DrawString(_font, string.Format("Score: {0}", _score), Vector2.One, Color.White);
            _spriteBatch.End();

            _guiManager.Draw(gameTime);

            base.Draw(gameTime);
        }
    }
}
