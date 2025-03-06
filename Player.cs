using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GarbanzoQuest
{
    public class InputOverride
    {
        public string Name;
        public float Time;
        public float Duration;

        public InputOverride(string name, float time, float duration = 0)
        {
            Name = name;
            Time = time;
            Duration = duration;
        }
    }

    public class Player : Controllable
    {
        public static Player PlayerOne = null;
        public static Player PlayerTwo = null;
        public static List<Player> Players = new List<Player>() { };
        public static bool PlayerTwoJustDied = false;

        public static bool ShowHearts = false;
        public static bool IsMovingBetweenScenes = false;
        public static bool JustTransitionedScenes = false;
        public static List<(string Filename, string Key, string Type)> FriendlyContainers = new List<(string, string, string)>() { };

        public static int MaxHealth = 2;
        public static bool IsGodMode = false;
        public static string DenyNoBadge = "";
        public static bool KeepSignifier => (MyGame.PlayerCount == 2 && SaveData.Current != null && SaveData.Current.Costume == SaveData.Current.CostumePlayerTwo);
        public static int Coins = 0;
        public static bool SkipCheckpoint = false;
        public static bool DisableBadges = false;

        public Color? TintOverride = null;
        public PaintColor PaintColor => PaintColor.PaintColors.Find(a => a.Name == Tint);
        public string Tint => (IsPlayerOne ? SaveData.Current?.Tint : SaveData.Current?.TintPlayerTwo);
        public Accessory Accessory => Accessory.Accessories.Find(a => a.Name == (IsPlayerOne ? SaveData.Current.Accessory : SaveData.Current.AccessoryPlayerTwo));
        public bool UseGrayScale => (TintOverride != null && TintOverride != Paint.White);

        bool didDoubleJump = false;
        float parryDuration = 0.65f;
        float parryActiveDuration = 0.2f;
        float parryTime = 0;
        bool didParry = false;

        public int SpriteOrder => IsPlayerOne ? 6 : 2;

        public bool IsMoonwalking => (Controller.MoonwalkStyle != "Toggle" && Controller.Moonwalk.IsHeld) || (Controller.MoonwalkStyle == "Toggle" && MoonwalkToggle);
        public bool MoonwalkToggle = false;
        Texture2D lockTexture = Lib.GetTexture("Lock");
        float lockAlpha = 0;
        Vector2 lockPosition;

        bool canGo = false;
        float blinkDuration = 0.25f;
        float blinkTime = 0.25f;
        bool blink = false;
        public bool Flicker => (
            (State == "Leap" && !didRecoverFromLeap)
                || (!Liver.Active && State != "Recoil" && Liver.Health > 0)
                || (State == "Bounce" && !canBounceFastFall)
            );

        public event Action JustTeleported;

        List<InputOverride> inputOverrides = new List<InputOverride>()
        {
            new InputOverride("Jump", 0.8499963f, 0.19999921f),
            new InputOverride("Right", 1.7999948f, 1.3499986f),
            new InputOverride("Left", 3.1333268f, 0.083333254f),
            new InputOverride("Spit", 3.6666596f, 0.083333254f),
            new InputOverride("Jump", 4.6333165f, 0.16666412f),
            new InputOverride("Right", 4.133324f, 0.84998703f),
            new InputOverride("Right", 6.399956f, 0.33332825f),
            new InputOverride("Spit", 6.8999486f, 0.08333206f),
            new InputOverride("Spit", 7.2666097f, 0.14999771f),
            new InputOverride("Right", 7.7499356f, 1.8666387f),
            new InputOverride("Right", 9.749906f, 0.6833229f),
            new InputOverride("Jump", 11.466546f, 0.16666412f),
            new InputOverride("Right", 11.166551f, 0.58332443f),
            new InputOverride("Left", 12.233201f, 0.36666107f),
            new InputOverride("Right", 12.633195f, 0.08333206f),
            new InputOverride("Spit", 13.116521f, 0.08333206f),
            new InputOverride("Right", 13.349851f, 0.566658f),
            new InputOverride("Spit", 14.04984f, 0.06666565f),
            new InputOverride("Jump", 14.683164f, 0.24999619f),
            new InputOverride("Right", 14.216504f, 1.2499809f),
            new InputOverride("Spit", 16.066477f, 0.099998474f),
            new InputOverride("Right", 16.399805f, 1.2666473f),
            new InputOverride("Left", 17.63312f, 0.08333206f),
            new InputOverride("Right", 19.649755f, 0.2666626f),
            new InputOverride("Jump", 20.349745f, 0.11666489f),
            new InputOverride("Right", 20.099749f, 0.44999313f),
            new InputOverride("Down", 21.199732f, 0.1333313f),
            new InputOverride("Right", 21.316397f, 0.84998703f),
            new InputOverride("Left", 22.433046f, 0.1333313f),
            new InputOverride("Up", 22.566378f, 0.63332367f),
            new InputOverride("Down", 24.36635f, 0.14999771f),
            new InputOverride("Right", 24.066355f, 1.9666367f),
            new InputOverride("Left", 26.016325f, 0.14999771f),
            new InputOverride("Right", 26.432985f, 0.18333054f),
            new InputOverride("Left", 26.666315f, 0.11666489f),
            new InputOverride("Spit", 27f, 1f),
            new InputOverride("Right", 28.066294f, 0.14999771f),
            new InputOverride("Up", 28.182959f, 0.6499901f),
            new InputOverride("Right", 28.349623f, 1.049984f),
            new InputOverride("Jump", 30.249594f, 0.1333313f),
            new InputOverride("Right", 29.949598f, 1.3833122f),
            new InputOverride("Left", 31.316244f, 0.099998474f),
        };

        public bool CanUseDoor => didReleaseInputSinceDoor && doorCooldownTime <= 0;
        public bool IsInControl = true;
        public bool DisableGravity = false;

        public bool IsFeetColliding => (GravityChanger.Direction.Y == 1 && Collider.Info.Down)
            || (GravityChanger.Direction.Y == -1 && Collider.Info.Up)
            || (GravityChanger.Direction.X == 1 && Collider.Info.Right)
            || (GravityChanger.Direction.X == -1 && Collider.Info.Left);

        public bool IsHeadColliding => (GravityChanger.Direction.Y == 1 && Mover.Velocity.Y < 0 && Collider.Info.Up)
            || (GravityChanger.Direction.Y == -1 && Mover.Velocity.Y > 0 && Collider.Info.Down)
            || (GravityChanger.Direction.X == 1 && Mover.Velocity.X < 0 && Collider.Info.Left)
            || (GravityChanger.Direction.X == -1 && Mover.Velocity.X > 0 && Collider.Info.Right);

        int facingDirection
        {
            get
            {
                int dir = 0;
                
                // Input
                if (!IsMoonwalking && State != "Leap")
                {
                    if (GravityChanger.IsVertical)
                    {
                        dir = (int)input.X;
                    }
                    else
                    {
                        dir = (int)input.Y;
                    }
                }

                // Sprite
                if (dir == 0)
                {
                    dir = Sprite.FlipHorizontally ? -1 : 1;
                    if (GravityChanger.Direction.Y == -1) dir *= -1;
                    if (GravityChanger.Direction.X == 1) dir *= -1;
                }

                return dir;
            }
        }
        // ---

        public Liver Liver;
        public HurtHandler HurtHandler;
        public PlayerCrushChecker CrushChecker;
        public Pusher Pusher;
        public UiHealthBar UiHealthBar;
        public UiBar StaminaBar;
        public PlayerCornerChecker PlayerCornerChecker;
        public SizeScaler SizeScaler;
        public Hurter Hurter;
        public QuickSwap QuickSwap;

        Vector2 quickSwapLockInput = new Vector2(0, 0);
        float quickSwapLockInputCooldown = 0;

        public PlayerGhost Ghost = null;

        public Player OtherPlayer;
        public bool QueueOtherPlayerRevive = false;
        public bool BubbleSafe = false;
        public bool BubbleEscape = false;

        Vector2 lerpTarget = Vector2.Zero;
        Entity warp = null;

        bool fellOffScreen = false;
        bool fellOffScreenAndInAir = false;
        bool fellOffScreenCanRecover = true;
        int fellOffScreenCount = 1;
        float fellRotation = 0f;

        int? focusRoutine = null;
        float focusRoutineTimer = 0;
        Scene oldScene = null;
        Scene newScene = null;
        List<Scene> crossoverScenes = new List<Scene>() { };
        CameraTarget cameraTarget = null;
        Vector2 playerDestination = new Vector2(0, 0);
        Vector2 playerDirection = new Vector2(0, 0);
        bool playerReachedDestination = false;
        Vector2 otherPlayerDestination = new Vector2(0, 0);
        Vector2 otherPlayerDirection = Vector2.Zero;
        bool otherPlayerReachedDestination = false;
        public static Vector2 CameraDestination = new Vector2(0, 0);
        Vector2 panDirection = new Vector2(0, 0);
        float panSpeed = 1.5f;

        Texture2D pixel;
        Texture2D ropePointTexture;
        Texture2D spiderweb;
        List<Vector2> ropePoints = new List<Vector2>() { };
        public static float RopeTime = 0;
        float ropeDuration = 2f;
        Color ropeColor = Color.White;
        public int RopeConsecutive = 0;

        public bool IsLeader => OtherPlayer?.IsAvailable != true ? true : new List<Player>() { this, OtherPlayer }
            .OrderByDescending(p => p.Transform.Position.Distance(SaveData.Current.Checkpoint.Position))
            .First() == this;
        bool lastIsLeader = true;

        bool isOnIce = false;
        public bool IsTouchingAntiGravity = false;
        public bool IsTouchingWater = false;
        float antiGravityTime = 0;
        float antiGravity2Time = 0;
        float antiGravityDuration = 0.1f;
        public bool JustExitedAntiGravity = false;
        bool wasTouchingWaterfall = false;
        Vector2 lastWaterInput = Vector2.Zero;

        float killTime = 0;
        public bool CanFocusScene = true;
        public bool Freeze = false;

        string identity = "PlayerOne";

        public int SpitStrength => SaveData.Current == null ? 1 : IsPlayerOne ? SaveData.Current.SpitStrength : SaveData.Current.SpitStrengthPlayerTwo;

        float gravity = CONSTANTS.GRAVITY;
        int gravitySign => GravityChanger.Direction.Y == 1 ? 1
            : GravityChanger.Direction.Y == -1 ? -1
            : GravityChanger.Direction.X == 1 ? 1
            : GravityChanger.Direction.X == -1 ? -1 : 1;
        float changedGravityTime = 0;
         Vector2 standardBottom => GravityChanger.Direction.Y == 1 ? Transform.Bottom
            : GravityChanger.Direction.Y == -1 ? Transform.Top
            : GravityChanger.Direction.X == 1 ? Transform.Right
            : GravityChanger.Direction.X == -1 ? Transform.Left : Transform.Bottom;
        Vector2 standardBottomLeft => GravityChanger.Direction.Y == 1 ? Transform.BottomLeft 
            : GravityChanger.Direction.Y == -1 ? Transform.TopLeft 
            : GravityChanger.Direction.X == 1 ? Transform.BottomRight 
            : GravityChanger.Direction.X == -1 ? Transform.BottomLeft : Transform.BottomLeft;
        Vector2 standardBottomRight => GravityChanger.Direction.Y == 1 ? Transform.BottomRight
            : GravityChanger.Direction.Y == -1 ? Transform.TopRight
            : GravityChanger.Direction.X == 1 ? Transform.TopRight
            : GravityChanger.Direction.X == -1 ? Transform.TopLeft : Transform.BottomRight;
        bool isFallingOffScreen => GravityChanger.Direction.Y == 1 ? Transform.Bottom.Y > MyGame.CurrentScene.CameraBottom.Y + 4
            : GravityChanger.Direction.Y == -1 ? Transform.Top.Y < MyGame.CurrentScene.CameraTop.Y - 4
            : GravityChanger.Direction.X == 1 ? Transform.Right.X > MyGame.CurrentScene.CameraRight.X + 4
            : GravityChanger.Direction.X == -1 ? Transform.Left.X < MyGame.CurrentScene.CameraLeft.X - 4 : Transform.Bottom.Y > MyGame.CurrentScene.CameraBottom.Y + 4;

        bool didReleaseInputX = true;
        bool didReleaseInputY = true;
        float runDustTime = 0;
        int lastRunDustDirection = 0;
        readonly float MIN_JUMP_SPEED = -160.25f;
        float minJumpSpeed = -160.25f;
        float jumpSpeed = -212.75f;
        float jumpFromWaterSpeed = -158.5f;
        float maxFallSpeed = CONSTANTS.MAX_FALL_SPEED;
        float moveSpeed = 80f;
        float runSpeed = 115f;
        Vector2 lockRunDirection = new Vector2(0, 0);
        // 1 never, 0 instant
        float upAcceleration = 0.003f;
        float downAcceleration = 0.000001f;
        float airAcceleration = 0.00045f;
        float iceAcceleration = 0.35f;
        float swimAcceleration = 0.000125f;
        float flutterFallSpeed = 35f;
        float flutterSpeed = -70f;
        float gravityGraceDuration = 0.1f;
        const float jumpBufferDuration = 0.1f;
        float jumpBuffer = 0;
        const float flutterBufferDuration = 0.1f;
        float flutterBuffer = 0;
        bool canDoubleJump = true;
        bool didLand = false;
        float recoilTime = 0;
        float recoilDuration = 0.55f;
        Vector2? recoilVelocity = null;
        Color lerpColor = Color.White;
        int rowLength => (int)(Sprite.Texture.Width / Sprite.TileSize.Value);

        float webbedDuration = 2f;
        float webbedTime = 0;
        bool slowedDown => webbedTime > 0 || Collider.CheckGridPoint(Transform.Position)?.Type == 9 || (State == "Swim" && BadgeIsHeld(Badge.Float));

        float pantsDuration = 5f;
        float pantsTime = 0;
        float pantsTurnTime = 0f;
        float pantsTurnDuration = 1f;
        int pantsDirection = 1;
        float originalWidth = 0;
        float originalHeight = 0;

        // swim/ water
        float swimSpeed = 80f;
        public Direction SwimDirection = Direction.Right;
        public Vector2 SwimInput;

        public string State = "Normal";
        string recoilReturnState = "Normal";
        string talkReturnState = "Normal";

        bool canShoot => spitTime <= 0
            && hairballTime <= 0
            && barkTime <= 0 && !isBarking
            && homingWadTime <= 0;

        // Spit
        bool canSpit => (spitInstance?.IsAvailable != true || spitInstance?.IsOnScreen() != true) && !isBarking && spitTime <= 0 && fellOffScreenCanRecover;
        // bool canSpit = true;
        Collider spitInstance = null;
        float spitDuration => LastHit == "Crumbly" ? IsRunning ? 0.1f : 0.17f : 0.2f;
        float spitTime = 0;
        public string LastHit = "";
        // Old spit
        Vector2 spitKickback = new Vector2(16f, 64f);
        float staminaMax = 6;
        float stamina = 6;
        float staminaRegen = 6;
        float staminaDisplayTime = 2;
        bool waitForStamina = false;

        float gravityGraceTime = 0;
        bool hadLeftGround = false;
        Transform lastSafePosition = null;
        bool didJump = true;
        public bool DidJumpFromSpring = false;

        int dir = 0;
        float moveDir = 0f;

        Vector2 input;

        int stopGrace = 0;

        List<String> spitSfx = new List<string>() { };

        // Run
        // public bool IsRunning => BadgeIsHeld(Badge.Run);
        // Toggle?
        public bool IsRunning = false;
        float runSpriteTime = 0;
        float runSpriteDuration = 0.1f;
        public bool lockRunEffects = false;
        bool disableRunEffects = false;
        float runningBump = 0;
        float runningBumpDuration = 0.05f;

        // Leap
        float leapCooldownTime;
        float leapCooldownDuration = 0.25f;
        bool canExitLeap = true;
        float leapSpeed = 80f;
        float leapJumpSpeed = -120f;
        bool didLandFromLeap = false;
        bool didRecoverFromLeap = false;
        bool queueJumpFromLeap = false;

        // Dash (Water Leap)
        float dashDuration = 1f;
        float dashTime = 0;
        Vector2 dashDirection = new Vector2(0, 0);
        bool didDashShake = false;
        float dashSpeed = 130f;

        // Bounce
        bool canEnterBounce = true;
        bool canExitBounce = true;
        float bounceRotationSpeed = 2.5f;
        bool canBounceFastFall = true;
        bool queueBounceFastFall = false;
        float bounceMoveAcceleration => 0.0045f;
        float bounceDoubleJumpSpeed = -120.25f;
        float bounceJumpSpeed = -190f;
        float bounceHitTime = 0;
        float bounceInvincibleCooldown = 0;

        // PingPong (Water Bounce)
        float pingPongDuration = 1f;
        float pingPongTime = 0;
        Vector2 pingPongDirection = new Vector2(0, 0);
        bool didPingPongShake = false;
        int? lastPingPongHitId = null;
        float pingPongSpeed = 130f;

        // Hairball
        bool canHairball => spitTime <= 0 && hairballTime <= 0 && !isBarking;
        Collider hairballInstance = null;
        float hairballDuration = 0.65f;
        float hairballTime = 0;

        // Bark
        bool canBark => spitTime <= 0
            && barkTime <= 0
            && !isBarking;
        int barkCount = 0;
        int barkMax = 3;
        bool isBarking = false;
        public static float BarkIntervalDuration = 0.1f;
        float barkIntervalTime = 0;
        float barkDuration => (barkMax * 1.15f) * 0.17f;
        float barkTime = 0;

        // Homing Wad
        bool canHomingWad => canShoot && homingWads.Where(h => h?.IsAvailable == true).Count() < HomingWadMax;
        float homingWadTime = 0;
        float homingWadDuration = 0.333f;
        int homingWadInterval = 1;
        public static readonly int HomingWadMax = 6;
        List<Entity> homingWads = Enumerable.Repeat((Entity)null, HomingWadMax).ToList();

        bool queueCheckpoint = false;
        bool queueDownCheckpoint = false;

        bool didForceJump = false;
        bool dampenGravity = false;
        bool didJumpFromWater = false;
        public Vector2 StoreVelocity = Vector2.Zero;

        float doorCooldownTime = 0;
        public float HurtTime = 0;
        public float HealTime = 0;

        public bool IsFluttering = false;

        public bool IsStupid = false;
        float stupidDuration = 9.5f;
        public float StupidTime = 0;
        float stupidEffectsDuration = 0.5f;
        float stupidEffectsTime = 1f;

        bool didReleaseInputSinceDoor = true;
        public bool ReachedDestination { get; private set; } = false;

        float pauseTime = 0;

        public bool TouchingCamera = false;

        // Break
        public bool CanUseBreakBadge => breakTime <= 0 && breakBehavior?.IsAvailable == true && breakBehavior.Charge > 0;
        float breakDuration = 0.1f;
        float breakTime = 0;
        BreakBehavior breakBehavior = null;

        float elapsed = 0;

        List<string> weaknesses = new List<string>();

        Vector2 startPosition;

        Transform target;
        Vector2? targetPosition;
        float targetSpeed = 60;
        int targetOffset = 24;
        bool targetAbsolute = false;
        bool targetGoOffLedges = true;

        List<UiSprite> heartsUi = new List<UiSprite>();
        Entity ui = null;

        bool safe = false;

        AnimationState animationState;
        AnimationState animationSwimGo => new AnimationState("SwimGo", 41, 42, true, slowedDown ? 10 : 20, new List<AnimationEvent>()
        {
            new AnimationEvent(42, _ =>
            {
                if (GarbanzoQuest.Talk.Letterbox?.IsAvailable != true && !MyGame.IsTalking) 
                    Factory.SfxPlayer(name: "Swim", volume: IsMoonwalking ? 0.15f : 0.1f, cutoffTime: 0.05f, pitch: IsMoonwalking ? Chance.Range(-0.4f, -0.3f) : Chance.Range(-0.1f, 0.1f));
                Factory.BubbleParticleEffect(Entity.Scene, Transform.Position);
            })
        });
        AnimationState animationSwim => new AnimationState("Swim", 37, 40, true, 4);

        public bool Scooched = false;

        Entity lowHealthTimer = null;

        float transportManualTime = 0;
        bool transportManual = false;
        Transform transportTarget;

        public bool BadgeIsReleased(Badge badge) =>
            badge != null && !IsStupid && !DisableBadges 
                && (
                    (Controller.InputProfile?.BadgeStyle == "Expert" && ExpertBadgeIsReleased(badge))
                        || (Controller.InputProfile?.BadgeStyle == "Normal" && 
                            (
                                (BadgeEquippedSlot(badge) == 1 && Controller.SpecialOne.IsReleased)
                                || (BadgeEquippedSlot(badge) == 2 && Controller.SpecialTwo.IsReleased)
                            )
                        )
                );

        public bool BadgeIsPressed(Badge badge) =>
            badge != null && !IsStupid && !DisableBadges 
                && (
                    (Controller.InputProfile?.BadgeStyle == "Expert" && ExpertBadgeIsPressed(badge))
                        || (Controller.InputProfile?.BadgeStyle == "Normal" && 
                            (
                                (BadgeEquippedSlot(badge) == 1 && Controller.SpecialOne.IsPressed)
                                    || (BadgeEquippedSlot(badge) == 2 && Controller.SpecialTwo.IsPressed)
                            )
                    )
                );
        public bool BadgeIsHeld(Badge badge) =>
            badge != null && !IsStupid && !DisableBadges
                && (
                    (Controller.InputProfile?.BadgeStyle == "Expert" && ExpertBadgeIsHeld(badge))
                        || (Controller.InputProfile?.BadgeStyle == "Normal" && 
                            (
                                (BadgeEquippedSlot(badge) == 1 && Controller.SpecialOne.IsHeld)
                                    || (BadgeEquippedSlot(badge) == 2 && Controller.SpecialTwo.IsHeld)
                            )
                        )
                );

        /// 1 or 2
        public int? BadgeEquippedSlot(Badge badge) => 
            FilterBadges.Enabled
                ? IsPlayerOne
                    ? (FilterBadges.BadgeOne == badge ? 1 : FilterBadges.BadgeTwo == badge ? 2 : null)
                    : (FilterBadges.BadgeOnePlayerTwo == badge ? 1 : FilterBadges.BadgeTwoPlayerTwo == badge ? 2 : null)
                : IsPlayerOne
                    ? (SaveData.Current?.BadgeOne == badge.Name ? 1 : SaveData.Current?.BadgeTwo == badge.Name ? 2 : null)
                    : (SaveData.Current?.BadgeOnePlayerTwo == badge.Name ? 1 : SaveData.Current?.BadgeTwoPlayerTwo == badge.Name ? 2 : null);
        public bool BadgeIsEquipped(Badge badge) => BadgeEquippedSlot(badge) != null;

        public bool ExpertBadgeIsReleased(Badge badge) => badge.IsUnlocked &&
            (Controller.SpecialOne.IsReleased && (ExpertBadgeCombos.ContainsKey(badge) && ExpertBadgeCombos[badge](this))
                || Controller.SpecialTwo.IsReleased && (ExpertBadgeCombosTwo.ContainsKey(badge) && ExpertBadgeCombosTwo[badge](this))
        );

        public bool ExpertBadgeIsPressed(Badge badge) => badge.IsUnlocked &&
            (Controller.SpecialOne.IsPressed && (ExpertBadgeCombos.ContainsKey(badge) && ExpertBadgeCombos[badge](this))
                || Controller.SpecialTwo.IsPressed && (ExpertBadgeCombosTwo.ContainsKey(badge) && ExpertBadgeCombosTwo[badge](this))
        );

        public bool ExpertBadgeIsHeld(Badge badge) => badge.IsUnlocked &&
            (Controller.SpecialOne.IsHeld && (ExpertBadgeCombos.ContainsKey(badge) && ExpertBadgeCombos[badge](this))
                || Controller.SpecialTwo.IsHeld && (ExpertBadgeCombosTwo.ContainsKey(badge) && ExpertBadgeCombosTwo[badge](this))
        );

        string expertLock = null;
        public bool ExpertUp() => expertLock == "Up" || (expertLock == null && Controller.Up.IsHeld);
        public bool ExpertDown() => expertLock == "Down" || (expertLock == null && Controller.Down.IsHeld);
        public bool ExpertLeft() => expertLock == "Left" || (expertLock == null && Controller.Left.IsHeld);
        public bool ExpertRight() => expertLock == "Right" || (expertLock == null && Controller.Right.IsHeld);
        public bool ExpertNeutral() => expertLock == "Neutral" || (expertLock == null && !Controller.Left.IsHeld && !Controller.Right.IsHeld && !Controller.Up.IsHeld && !Controller.Down.IsHeld);
        public bool ExpertHorizontalSide() => expertLock == "Left" || expertLock == "Right" || (expertLock == null && (Controller.Left.IsHeld || Controller.Right.IsHeld) && !Controller.Up.IsHeld && !Controller.Down.IsHeld);
        public bool ExpertVerticalSide() => expertLock == "Up" || expertLock == "Down" || (expertLock == null && (Controller.Up.IsHeld || Controller.Down.IsHeld) && !Controller.Left.IsHeld && !Controller.Right.IsHeld);
        public bool RelativeExpertUp() => GravityChanger.Direction.Y == 1 ? ExpertUp() : GravityChanger.Direction.Y == -1 ? ExpertDown() : GravityChanger.Direction.X == 1 ? ExpertLeft() : GravityChanger.Direction.X == -1 ? ExpertRight() : false;
        public bool RelativeExpertDown() => GravityChanger.Direction.Y == 1 ? ExpertDown() : GravityChanger.Direction.Y == -1 ? ExpertUp() : GravityChanger.Direction.X == 1 ? ExpertRight() : GravityChanger.Direction.X == -1 ? ExpertLeft() : false;
        public bool RelativeExpertSide() => GravityChanger.Direction.Y != 0 ? ExpertHorizontalSide() : ExpertVerticalSide();

        public Dictionary<Badge, Func<Player, bool>> ExpertBadgeCombos = new Dictionary<Badge, Func<Player, bool>>()
        {
            { Badge.Bark, (Player player) => player.RelativeExpertUp() },
            { Badge.Float, (Player player) => player.RelativeExpertDown() },
            { Badge.Leap, (Player player) => player.RelativeExpertSide() },
            { Badge.Bounce, (Player player) => player.ExpertNeutral() }
        };        
        public Dictionary<Badge, Func<Player, bool>> ExpertBadgeCombosTwo = new Dictionary<Badge, Func<Player, bool>>()
        {
            { Badge.Break, (Player player) => player.RelativeExpertUp() },
            { Badge.Parry, (Player player) => player.RelativeExpertDown() },
            { Badge.Run, (Player player) => player.RelativeExpertSide() },
            { Badge.Hairball, (Player player) => player.ExpertNeutral() }
        };

        public Badge BadgeOne => IsPlayerOne
            ? Badge.AllBadges.Find(b => b.Name == SaveData.Current?.BadgeOne)
            : Badge.AllBadges.Find(b => b.Name == SaveData.Current?.BadgeOnePlayerTwo);

        public Badge BadgeTwo => IsPlayerOne
            ? Badge.AllBadges.Find(b => b.Name == SaveData.Current?.BadgeTwo)
            : Badge.AllBadges.Find(b => b.Name == SaveData.Current?.BadgeTwoPlayerTwo);

        public void BadgeEquip(Badge badge, int slot)
        {
            if (FilterBadges.Enabled)
            {
                if (IsPlayerOne) FilterBadges.BadgeOne = badge;
                else FilterBadges.BadgeOnePlayerTwo = badge;
            }
            else
            {
                if (IsPlayerOne)
                {
                    if (slot == 1) SaveData.Current.BadgeOne = badge?.Name;
                    else SaveData.Current.BadgeTwo = badge?.Name;
                }
                else
                {
                    if (slot == 1) SaveData.Current.BadgeOnePlayerTwo = badge?.Name;
                    else SaveData.Current.BadgeTwoPlayerTwo = badge?.Name;
                }
            }
        }

        // Cycle

        public override void Setup()
        {
            base.Setup();

            identity = Entity.Tags.Contains("PlayerOne") ? "PlayerOne" : "PlayerTwo";

            Sprite = Entity.GetComponent<Sprite>();
            Animator = Entity.GetComponent<Animator>();
            Mover = Entity.GetComponent<Mover>();
            Transform = Entity.GetComponent<Transform>();
            Collider = Entity.GetComponent<Collider>();
            Liver = Entity.GetComponent<Liver>();
            HurtHandler = Entity.GetComponent<HurtHandler>();
            Shaker = Entity.GetComponent<Shaker>();
            Shatterer = Entity.GetComponent<Shatterer>();
            CrushChecker = Entity.GetComponent<PlayerCrushChecker>();
            Pusher = Entity.GetComponent<Pusher>();
            UiHealthBar = Entity.GetComponent<UiHealthBar>();
            PlayerCornerChecker = Entity.GetComponent<PlayerCornerChecker>();
            SizeScaler = Entity.GetComponent<SizeScaler>();
            IconCollector = Entity.GetComponent<IconCollector>();
            Hurter = Entity.GetComponent<Hurter>();
            QuickSwap = Entity.GetComponent<QuickSwap>();

            originalWidth = Transform.Width;
            originalHeight = Transform.Height;

            CheckCostume();
            HurtHandler.HitSound = $"{PlayerProfile.Costume}Hit";

            // ===

            pixel = Lib.GetTexture("Pixel");
            spiderweb = Lib.GetTexture("Spiderweb");
            ropePointTexture = Lib.GetTexture("RopePoint");

            Entity.OnBeforeDestroy += _ =>
            {
                Players.Remove(this);
                if (IsPlayerOne) PlayerOne = null;
                else PlayerTwo = null;
                // if (ui?.IsAvailable == true)
                // {
                //     ui.Destroy();
                // }
            };

            Liver.OnBeforeTerminate += (Liver liver, int factor, Hurter hurter) =>
            {
                UiHealthBar.Popup();

                StatListener.IncrementKOs();
                StatListener.IncrementHurts();
                Collider.Active = false;
                Collider.DoesCollideWithGround = false;
                Mover.Active = false;
                Sprite.Order = -10000;

                if (breakBehavior?.IsAvailable == true) breakBehavior.Shatter();

                Camera.Shake(2);
                Controller.Rumble(0.6f, 0.6f);
            };

            Liver.OnGainedHealth += (Liver liver, int factor) =>
            {
                HealTime = 1.5f;
                HurtTime = 0;
            };

            Liver.OnAfterSurvivedDamage += (Liver liver, int factor, Hurter hurter) =>
            {
                SurviveDamage(factor, hurter, true);
            };

            Liver.Terminate = (Liver l) =>
            {
                Factory.StarParticleEffect(Entity.Scene, Transform.Position);

                HurtTime = 0.6f;
                HealTime = 0;

                if (SaveData.Current?.Respawn == "Beginning")
                {
                    Menu.Disable = true;
                    Factory.Music("Dead", false);
                    Factory.SfxPlayer(name: "Wrecked", pitch: -1f);
                    Factory.OneShotTimer(scene: MyGame.PlayerContainer, useModifiedDelta: false, duration: 1f, end: _ =>
                    {
                        Factory.FadingText(
                            position: new Vector2(CONSTANTS.VIRTUAL_WIDTH_PLAY / 2f, CONSTANTS.VIRTUAL_HEIGHT_PLAY / 2f),
                            text: Dial.Key($"GAME_OVER_{Chance.Range(1, 5)}"),
                            color: Paint.AbsoluteRed,
                            outlineColor: Paint.Black,
                            scale: 2,
                            batch: BatchType.Absolute,
                            duration: 6,
                            shake: true,
                            order: -9999
                        );
                    });
                }
                else if (NoHitReward.Scene?.HasValue() == true || SaveData.Current?.Respawn == "Level")
                {
                    Factory.Music("");
                    Factory.SfxPlayer(name: "Wrecked", pitch: -0.5f);
                    Factory.OneShotTimer(scene: MyGame.PlayerContainer, useModifiedDelta: false, duration: 0.5f, end: _ =>
                    {
                        Factory.SfxPlayer(name: "DeadQuick");
                    });
                }
                else
                {
                    Factory.SfxPlayer(name: "Wrecked");
                }

                IsInControl = false;
                HurtHandler.Active = false;
                Liver.Active = false;
                State = "Stop";
                RecoilStart();

                if (SaveData.Current?.SharedHitPoints == true)
                {
                    MyGame.Entities.Find(e => e.Tags.Contains("CameraTarget")).Active = false;

                    if (OtherPlayer?.IsAvailable == true)
                    {
                        if (Entity.Tags.Contains("PlayerTwo"))
                        {
                            RopeTime = OtherPlayer.ropeDuration;
                            OtherPlayer.ropeColor = Paint.Pink;
                        }
                        else
                        {
                            RopeTime = ropeDuration;
                            ropeColor = Paint.Pink;
                        }

                        OtherPlayer.IsInControl = false;
                        OtherPlayer.RecoilStart();
                    }
                }

                Shaker.ShakeX();

                if (!IsPlayerOne) Player.PlayerTwoJustDied = true;

                if (OtherPlayer?.IsAvailable == true)
                {
                    OtherPlayer.QueueOtherPlayerRevive = true;
                }

                Routine.New(r => r.Elapsed >= 0.6f && !IsMovingBetweenScenes)
                    .Then(r =>
                    {
                        HurtTime = 0;
                        if (SaveData.Current?.Respawn == "Beginning")
                        {
                            // Factory.SfxPlayer(name: "WreckedFall", pitch: -1f);
                            Factory.SfxPlayer(name: "GhostLaugh", pitch: -1f);
                        }
                        else if (NoHitReward.Scene?.HasValue() == true || SaveData.Current?.Respawn == "Level")
                        {
                            Factory.SfxPlayer(name: "WreckedFall", pitch: -0.5f);
                        }
                        else
                        {
                            Factory.SfxPlayer(name: "WreckedFall");
                        }

                        Shatterer.Shatter();
                        Entity.Destroy();

                        if (SaveData.Current?.SharedHitPoints == true)
                        {
                            if (OtherPlayer?.IsAvailable == true)
                            {
                                OtherPlayer.Shatterer.Shatter();
                                OtherPlayer.Entity.Destroy();
                            }
                        }

                        return true;
                    })
                    .Then(r => 
                    {
                        if (IsMovingBetweenScenes)
                        {
                            r.Cancel();
                            return false;
                        }

                        return r.Elapsed >= 0.4f;
                    })
                    .Then(r =>
                    {  
                        if (OtherPlayer != null && OtherPlayer.Liver.Health > 0 
                            && (
                                (IsPlayerOne && (PlayerOne?.IsAvailable != true || PlayerOne.Liver.Health <= 0))
                                    || (!IsPlayerOne && (PlayerTwo?.IsAvailable != true || PlayerTwo.Liver.Health <= 0))
                            )
                        )
                        {
                            Ghost = Factory.PlayerGhost(position: Transform.Position, playerProfile: PlayerProfile).GetComponent<PlayerGhost>();
                        }
                        return true;
                    });
            };

            HurtHandler.DoNo = (Transform col) =>
            {
                if (State == "Talk" || State == "Stop" || State == "Command" || State == "Launch" || State == "Target" || MyGame.IsTalking) return;
                if (fellOffScreen && ((((GravityChanger.IsVertical && Mover.Velocity.Y.Sign() != GravityChanger.Direction.Y) || (GravityChanger.IsHorizontal && Mover.Velocity.X.Sign() != GravityChanger.Direction.X)) && !IsTouchingWater) || (IsTouchingWater && State == "Recoil"))) return;

                bool wasStupid = IsStupid;

                Liver.LoseHealth(fellOffScreenCount);
                if (Liver.Health <= 0) 
                {
                    Mover.Velocity = Vector2.Zero;
                    Collider.DoesCollideWithGround = false;
                    return;
                }

                fellOffScreen = true;
                fellOffScreenAndInAir = true;
                fellOffScreenCount *= 2;
                fellRotation = 0;
                if (!IsTouchingWater) NormalStart();
                Collider.Info.BakedDown = false; Collider.Info.ActorCollisionDown.Clear();
                Collider.Info.BakedUp = false; Collider.Info.ActorCollisionUp.Clear();
                Collider.Info.BakedRight = false; Collider.Info.ActorCollisionRight.Clear();
                Collider.Info.BakedLeft = false; Collider.Info.ActorCollisionLeft.Clear();

                isBarking = false;

                if (IsTouchingWater)
                {
                    if (col != null)
                    {
                        if (col.Entity.Tags.Contains("Up")) Mover.Velocity = new Vector2(0, -100f);
                        else if (col.Entity.Tags.Contains("Down")) Mover.Velocity = new Vector2(0, 100f);
                        else if (col.Entity.Tags.Contains("Right")) Mover.Velocity = new Vector2(100f, 0);
                        else if (col.Entity.Tags.Contains("Left")) Mover.Velocity = new Vector2(-100f, 0);
                    }
                }
                else
                {
                    if (col != null)
                    {
                        col.Entity.GetComponent<Shaker>()?.ShakeX();
                        Hurter colHurter = col.Entity.GetComponent<Hurter>();
                        if (colHurter?.IsAvailable == true)
                        {
                            colHurter.LandedHit(colHurter, Entity);
                        }

                        if (lastSafePosition != null && !col.Entity.Tags.Contains("NoRelief"))
                        {
                            FallRelief();
                        }
                        else
                        {
                            if (col.Entity.Tags.Contains("Up")) Mover.Velocity = new Vector2(0, -125 * (GravityChanger.Direction.Y == 1 ? 2 : 1));
                            else if (col.Entity.Tags.Contains("Down")) Mover.Velocity = new Vector2(0, 125 * (GravityChanger.Direction.Y == -1 ? 2 : 1));
                            else if (col.Entity.Tags.Contains("Left")) Mover.Velocity = new Vector2(-125 * (GravityChanger.Direction.X == 1 ? 2 : 1), 0);
                            else if (col.Entity.Tags.Contains("Right")) Mover.Velocity = new Vector2(125 * (GravityChanger.Direction.X == -1 ? 2 : 1), 0);
                            else if (col.Entity.Tags.Contains("Out")) Mover.Velocity = ((Transform.Position - col.Position).Normalized() * 100f) - (GravityChanger.Direction * 100);
                            else Mover.Velocity = -GravityChanger.Direction * 300f;

                            if ((col.Entity.Tags.Contains("Up") || col.Entity.Tags.Contains("Down")) && GravityChanger.Direction.Y == 0) Mover.Velocity -= GravityChanger.Direction * 100f;
                            if ((col.Entity.Tags.Contains("Left") || col.Entity.Tags.Contains("Right")) && GravityChanger.Direction.X == 0) Mover.Velocity -= GravityChanger.Direction * 100f;
                            Mover.Velocity += (col.Entity.GetComponent<Mover>()?.Velocity * 2f) ?? Vector2.Zero;
                        }
                    }
                    else if (lastSafePosition != null)
                    {
                        FallRelief();
                    }
                    else
                    {
                        Mover.Velocity = -GravityChanger.Direction * 300f;
                    }

                    if (wasStupid)
                    {
                        RecoilStart(duration: 0.9f);
                    }
                }
            };
        }

        public override void Start()
        {
            base.Start();

            Refresh();

            lockPosition = Transform.Position;

            Player.ShowHearts = true;

            weaknesses = HurtHandler.Weaknesses;

            ChangeGravity(GravityChanger.Direction, Vector2.Zero);

            cameraTarget = MyGame.GetComponent<CameraTarget>();

            startPosition = Transform.Position;

            IsMovingBetweenScenes = false;

            if (MyGame.CurrentZone == "Title") PlayRecording();

            if (IsPlayerOne) PlayerOne = this;
            else PlayerTwo = this;
            Players.Add(this);

            CheckOtherPlayer();

            if (MyGame.PlayerCount == 2)
            {
                if (OtherPlayer?.IsAvailable != true || OtherPlayer.Liver.Health <= 0) QueueOtherPlayerRevive = true;
            }

            if ((PlayerProfile.Index != 2 || OtherPlayer?.Entity.IsAvailable != true))
            {
                if (SkipCheckpoint)
                {
                    SkipCheckpoint = false;
                } 
                else
                {
                    DestroyCrumblies();
                    Place.Checkpoint();
                }
            }

            // Just stand if talking
            if (MyGame.IsTalking)
            {
                ChangeState("Talk");
            }

            Factory.PlayerOffScreen(Entity.Scene, Transform.Position, this);

            if (SaveData.Current != null && SaveData.Current?.Recover != "Room")
            {
                if (SaveData.Current.StoredHealth <= 0) SaveData.Current.StoredHealth = SaveData.Current.HitPoints;
                if (SaveData.Current.StoredHealthPlayerTwo <= 0) SaveData.Current.StoredHealthPlayerTwo = SaveData.Current.HitPointsPlayerTwo;
                Liver.Health = IsPlayerOne ? SaveData.Current.StoredHealth : SaveData.Current.StoredHealthPlayerTwo;
            }

            // HurtHandler.TriggerInvincibility(99999f);
            // safe = true;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            // if (lastSafePosition != null)
            // {
            //     Shapes.DrawSprite(texture: pixel, position: lastSafePosition.Value, origin: new Vector2(0.5f, 0.5f), scale: new Vector2(8, 8), color: Paint.Gray);
            // }

            // Draw Rope

            if (RopeTime > 0 && MyGame.PlayerCount == 2 && OtherPlayer?.IsAvailable == true && Entity.Tags.Contains("PlayerOne"))
            {
                foreach (Vector2 point in ropePoints)
                {
                    Vector2 o = Utility.Shake();

                    spriteBatch.Draw(
                        ropePointTexture,
                        Transform.Position + point + o,
                        null,
                        Paint.White * (RopeTime / ropeDuration),
                        0f,
                        new Vector2(4),
                        new Vector2(1),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // Draw web

            if (webbedTime > 0)
            {
                spriteBatch.Draw(
                    texture: spiderweb,
                    position: Transform.Position + new Vector2(Chance.Range(-1, 2), Chance.Range(-1, 2)),
                    sourceRectangle: null,
                    color: Color.White * (webbedTime / webbedDuration),
                    rotation: 0f,
                    origin: new Vector2(spiderweb.Width / 2f, spiderweb.Height / 2f),
                    scale: new Vector2(1, 1),
                    effects: SpriteEffects.None,
                    layerDepth: 0f
                );
            }

            // Shapes.DrawSprite(
            //     texture: lockTexture,
            //     position: lockPosition,
            //     origin: new Vector2(6),
            //     color: Paint.White * 0.35f * lockAlpha,
            //     rotation: GravityChanger.Rotation
            // );
        }

        public void FallRelief()
        {
            Vector2 parentVelocity = lastSafePosition.Entity.GetComponent<Collider>().Transporters.Aggregate(new Vector2(0f, 0f), (acc, t) => acc += t.Mover.CombinedVelocity);
            if (GravityChanger.IsVertical) Mover.Velocity.X = 0;
            else Mover.Velocity.Y = 0;

            if (GravityChanger.Direction.Y == 1) 
            {
                Mover.Velocity.Y = MathF.Min(
                    jumpSpeed * 1.25f, 
                    Utility.BallTrajectory(Transform.Bottom, lastSafePosition.Position + parentVelocity, 24).Y
                );
            }
            else if (GravityChanger.Direction.Y == -1) 
            {
                Mover.Velocity.Y = MathF.Max(
                    -jumpSpeed * 1.25f, 
                    Utility.BallTrajectory(Transform.Top, lastSafePosition.Position + parentVelocity, 24).Y
                );
            }
            else if (GravityChanger.Direction.X == 1) 
            {
                Mover.Velocity.X = MathF.Min(
                    jumpSpeed * 1.25f, 
                    Utility.BallTrajectory(Transform.Right, lastSafePosition.Position + parentVelocity, 24).X
                );
            }
            else if (GravityChanger.Direction.X == -1) 
            {
                Mover.Velocity.X = MathF.Max(
                    -jumpSpeed * 1.25f, 
                    Utility.BallTrajectory(Transform.Left, lastSafePosition.Position + parentVelocity, 24).X
                );
            }
        }

        public void ChangeGravity(Vector2 direction, Vector2 lastDirection)
        {
            if (direction == lastDirection) return;
            didForceJump = true;

            if (State != "Swim")
            {
                if (GravityChanger.Direction.Y == 1 && lastDirection.Y == -1) Sprite.FlipHorizontally = !Sprite.FlipHorizontally;
                if (GravityChanger.Direction.Y == -1 && lastDirection.Y == 1) Sprite.FlipHorizontally = !Sprite.FlipHorizontally;
            }

            if (GravityChanger.IsVertical)
            {
                Transform.Width = 8;
                Transform.Height = 12;
            }
            else if (GravityChanger.IsHorizontal)
            {
                Transform.Width = 12;
                Transform.Height = 8;
            }

            if (lastDirection != Vector2.Zero)
            {
                int s = 0;
                while (s <= 100)
                {
                    s++;
                    bool top = Collider.IsTouching(Transform.Top);
                    bool bottom = Collider.IsTouching(Transform.Bottom);
                    bool left = Collider.IsTouching(Transform.Left);
                    bool right = Collider.IsTouching(Transform.Right);
                    if (top) Transform.Position.Y += 1;
                    else if (bottom) Transform.Position.Y -= 1;
                    if (left) Transform.Position.X += 1;
                    else if (right) Transform.Position.X -= 1;

                    if (!(top || bottom || left || right)) break;
                }

                Mover.Velocity = Vector2.Zero;
            }
        }

        public void Refresh()
        {
            spitSfx.Clear();
            spitSfx.Add($"{PlayerProfile.Costume}Spit");
            if (Lib.GetSoundEffect($"{PlayerProfile.Costume}Spit2") != null) spitSfx.Add($"{PlayerProfile.Costume}Spit2");
            if (Lib.GetSoundEffect($"{PlayerProfile.Costume}Spit3") != null) spitSfx.Add($"{PlayerProfile.Costume}Spit3");
            if (Lib.GetSoundEffect($"{PlayerProfile.Costume}Spit4") != null) spitSfx.Add($"{PlayerProfile.Costume}Spit4");
        }

        float flickerDuration = 0.075f;
        float flickerTime = 0f;
        bool flicker = false;
        bool exitFlicker = true;
        void RefreshColor()
        {
            Color color = Paint.White;
            if (angryColor != Paint.White) color = angryColor;
            if (Tint == "Dynamic") color = IsPlayerOne ? Paint.DynamicTwo : Paint.Dynamic;
            if (TintOverride != null) color = TintOverride.Value;

            // Hit
            if (HurtHandler.IsBlinking)
            {
                Sprite.Color = Paint.Black;
            }
            // Flicker
            else if (Flicker)
            {
                flickerTime += Time.Delta;
                if (flickerTime <= 0f)
                {
                    flicker = !flicker;
                    flickerTime = 0.5f;
                }

                Sprite.Order = -10;

                // Sprite.Color = flicker
                //     ? Sprite.Color.MoveTowardsWithSpeed(Paint.Gray * 0.25f, 2000f)
                //     : Sprite.Color.MoveTowardsWithSpeed(Paint.White, 2000f);

                Sprite.Effect = Lib.GetEffect("White");
                Sprite.EffectParameters["Time"] = new ComponentEffectParameter(MathF.Sin(flickerTime * 10).Abs() / 2f);
                // Sprite.EffectParameters["Alpha"] = new ComponentEffectParameter(1f);
                Sprite.EffectParameters["Alpha"] = new ComponentEffectParameter(0.5f + MathF.Sin(flickerTime * 10).Abs() / 2f);

                // Sprite.Color = flicker
                //     ? Paint.Gray
                //     : Paint.White;

                // Sprite.Color = Paint.Gray;

                exitFlicker = false;
            }
            // Normal
            else
            {
                if (!exitFlicker)
                {
                    if (State == "Normal") Shaker.ShakeY(2f);
                    Factory.ShineParticleEffect(Entity.Scene, Transform.Position);
                    color = angryColor = Sprite.Color = Paint.White;
                    Sprite.Effect = null;
                    exitFlicker = true;
                    flickerTime = 0;
                    Sprite.Order = SpriteOrder;
                }
                Sprite.Color = color;
            }

            if (UseGrayScale) Sprite.Texture = PlayerProfile.PlayerTextureGrayScale;
            else Sprite.Texture = PlayerProfile.PlayerTexture;
        }

        public override void PersistentUpdate()
        {
            base.PersistentUpdate();

            HurtTime -= Time.Delta;
            HealTime -= Time.Delta;
            if (HealTime < 0) HealTime = 0;
        }

        public override void Update()
        {
            base.Update();

            if (Controller.MoonwalkStyle == "Toggle")
            {
                if (Controller.Moonwalk.IsPressed)
                {
                    MoonwalkToggle = !MoonwalkToggle;
                    if (MoonwalkToggle)
                    {
                        Shaker.ShakeX();
                        Factory.DustParticleEffect(position: Transform.Position, amount: 2);
                        Factory.SfxPlayer(name: "Swing", volume: 0.6f);
                    }
                    else
                    {
                        Shaker.ShakeY();
                        Factory.SfxPlayer(name: "Swing", pitch: -0.5f, volume: 0.6f);
                    }
                }
                // lockAlpha = lockAlpha.MoveOverTime(MoonwalkToggle ? 1f : 0f, 0.1f);
                // lockPosition = lockPosition.MoveOverTime(
                //     Transform.Position
                //         + (-GravityChanger.Direction * 14)
                //         + new Vector2(GravityChanger.Direction.Y * 8 * -(facingDirection), GravityChanger.Direction.X * 8 * -(facingDirection))
                //         + new Vector2(MathF.Sin(Time.Elapsed * 4) * 1, MathF.Cos(Time.Elapsed * 2) * 1),
                //     0.000001f
                // );
            }
            else
            {
                lockAlpha = 0;
                MoonwalkToggle = false;

                if (Controller.Moonwalk.IsPressed)
                {
                    Shaker.ShakeX(1f);
                    Factory.DustParticleEffect(position: Transform.Position, amount: 1);
                    Factory.SfxPlayer(name: "Swing", volume: 0.3f);
                }

                if (Controller.Moonwalk.IsReleased)
                {
                    Shaker.ShakeY(1f);
                    Factory.SfxPlayer(name: "Swing", pitch: -0.5f, volume: 0.3f);
                }
            }

            if (IsMoonwalking)
            {
                if (expertLock == null)
                {
                    if (Controller.Up.IsHeld) expertLock = "Up";
                    else if (Controller.Down.IsHeld) expertLock = "Down";
                    else if (Controller.Left.IsHeld) expertLock = "Left";
                    else if (Controller.Right.IsHeld) expertLock = "Right";
                    else expertLock = "Neutral";
                }
            }
            else
            {
                expertLock = null;
            }

            if (safe)
            {
                if (Controller.Buttons.Any(b => b.IsHeld && b.Name != "Start"))
                {
                    safe = false;
                    HurtHandler.TriggerInvincibility(0.1f);
                    Shaker.ShakeY();
                    Factory.SfxPlayer(name: "IceSlip", pitch: 0.75f, volume: 0.35f, position: Transform.Position);
                }
            }

            if (DisableBadges && (Controller.SpecialOne.IsPressed || Controller.SpecialTwo.IsPressed))
            {
                NotNow();
            }

            TouchingCamera = false;

            RopeTime -= Time.Delta;
            transportManualTime -= Time.ModifiedDelta;

            // if (IsPlayerOne != true && Input.IsPressed("F6")) BubbleStart();

            if ((State == "Normal" || State == "Leap" || State == "Stand") && !IsGodMode && !fellOffScreen && !Freeze)
            {
                Sprite.Rotation = Sprite.Rotation.MoveOverTime(GravityChanger.Rotation, 0.01f);
            }

            Transform no = Collider.Info.ActorCollisions.Find(c => c.Entity.Tags.Contains("No"));

            // if (!lastIsLeader && IsLeader) Factory.LeaderIcon(Entity.Scene, Transform);
            // lastIsLeader = IsLeader;

            // Jumping on platform kills jump, if i don't do this
            if (Mover.Velocity.Y < 0) { Collider.Info.BakedDown = false; Collider.Info.ActorCollisionDown.Clear(); }
            if (Mover.Velocity.Y > 0) { Collider.Info.BakedUp = false; Collider.Info.ActorCollisionUp.Clear(); }
            if (Mover.Velocity.X < 0) { Collider.Info.BakedRight = false; Collider.Info.ActorCollisionRight.Clear(); }
            if (Mover.Velocity.X > 0) { Collider.Info.BakedLeft = false; Collider.Info.ActorCollisionLeft.Clear(); }

            if ((BadgeOne?.Name != "QuickSwap" && BadgeIsHeld(BadgeOne)) || (BadgeTwo?.Name != "QuickSwap" && BadgeIsHeld(BadgeTwo)))
            {
                DenyNoBadge = MyGame.CurrentZone;
            }

            if (new List<string>() { "Normal", "Swim", "Bounce", "PingPong", "Dash" }.Contains(State) && !fellOffScreen)
            {
                if (BadgeIsPressed(Badge.Run))
                {
                    IsRunning = !IsRunning;
                    lockRunDirection = Vector2.Zero;
                    if (!IsRunning) Factory.SfxPlayer(name: "Swing", pitch: Chance.Range(-0.5f, -0.25f), position: Transform.Position);
                    else Factory.SfxPlayer(Entity.Scene, "RunStart", Transform.Position, pitch: Chance.Range(-0.25f, 0.25f));
                }
            }

            if (IsRunning)
            {
                if (State != "Swim")
                {
                    if (IsFeetColliding)
                    {
                        if ((GravityChanger.IsVertical && (Collider.Info.Left || Collider.Info.Right)) || (GravityChanger.IsHorizontal && (Collider.Info.Up || Collider.Info.Down)))
                        {
                            runningBump += Time.ModifiedDelta;
                            if (runningBump >= runningBumpDuration)
                            {
                                Vector2 velocity = Vector2.Zero;
                                if (GravityChanger.IsVertical) velocity = new Vector2(50 * -Mover.Velocity.X.Sign(), -100 * GravityChanger.Direction.Y);
                                else velocity = new Vector2(-100 * GravityChanger.Direction.X, 50 * -Mover.Velocity.Y.Sign());
                                RecoilStart(duration: 0.4f, velocity: velocity);
                                Factory.DustParticleEffect(position: Transform.Position);
                                Factory.SfxPlayer(name: "Bump", pitch: Chance.Range(-0.5f, 0f), position: Transform.Position);
                                runningBump = 0;
                            }
                        }
                        else
                        {
                            runningBump = 0;
                        }
                    }
                }
            }

            if (isBarking || State == "Recoil" || !fellOffScreenCanRecover) Sprite.Position = new Vector2(Chance.Range(-2f, 2f), Chance.Range(-2f, 2f));
            else if (IsTouchingAntiGravity)
            {
                antiGravityTime -= Time.Delta;
                antiGravity2Time -= Time.Delta;
                if (antiGravityTime <= 0)
                {
                    Factory.ParticleEffect(
                        position: Transform.Position,
                        spriteTemplate: new Sprite(pixel, scale: new Vector2(Chance.Range(1, 5)), color: Paint.White),
                        fadeOverTime: true
                    );

                    if (Controller.Jump.IsHeld)
                    {
                        Factory.ParticleEffect(
                            position: Transform.Position,
                            spriteTemplate: new Sprite("MiniStar", 8, color: Paint.HexToColor("#fad6ff")),
                            fadeOverTime: true,
                            amount: 1,
                            duration: 2f,
                            speed: new Range(0.5f)
                        );
                    }
                    
                    if (GarbanzoQuest.Talk.Letterbox?.IsAvailable != true && !MyGame.IsTalking)
                    {
                        Factory.SfxPlayer(name: "Charm", pitch: Controller.Jump.IsHeld ? Chance.Range(-0.5f, -0.25f) : Chance.Range(-1f, -0.75f), position: Transform.Position, volume: 0.2f, cutoffTime: 0.25f);
                        if (Controller.Jump.IsHeld) Controller.Rumble(0.1f, 0.1f);
                        // else Controller.Rumble(0.05f, 0.05f);
                    }
                    antiGravityTime = antiGravityDuration;
                }

                if (antiGravity2Time <= 0)
                {
                    antiGravity2Time = antiGravityDuration * 0.5f;
                    Sprite.Position = Controller.Jump.IsHeld 
                        ?  new Vector2(Chance.Range(-0.5f, 0.5f), Chance.Range(-0.5f, 0.5f))
                        : new Vector2(Chance.Range(-0.25f, 0.25f), Chance.Range(-0.25f, 0.25f));
                }
            }
            // else if (spitTime > 0) Sprite.Position = new Vector2(Chance.Range(-0.25f, 0.25f), Chance.Range(-0.25f, 0.25f));
            else Sprite.Position = Vector2.Zero;

            if (Controller.Transport.IsPressed)
            {
                TransportStart(true);
            }

            Hurter.Damage = SpitStrength;

            if (MyGame.IsDevEnabled)
            {
                if (Input.IsHeld("LeftControl") && Input.IsPressed("G") && (identity == "PlayerOne" || OtherPlayer?.IsAvailable == false) && MyGame.CurrentZone != "Title")
                {
                    IsGodMode = !IsGodMode;
                    if (IsGodMode)
                    {
                        Factory.SfxPlayer(name: "MenuAccept", pitch: -1f);
                        Shaker.ShakeX();
                        Mover.Velocity = Vector2.Zero;
                    }
                    else if (Liver.Health > 0)
                    {
                        Factory.SfxPlayer(name: "MenuDeny", pitch: -1f);
                        Sprite.Rotation = GravityChanger.Rotation;
                        Liver.Active = true;
                        HurtHandler.Active = true;
                        CrushChecker.Active = true;
                        Collider.DoesCollideWithGround = true;

                        if (OtherPlayer?.IsAvailable == true)
                        {
                            OtherPlayer.Liver.Active = true;
                            OtherPlayer.HurtHandler.Active = true;
                            OtherPlayer.CrushChecker.Active = true;
                            OtherPlayer.Collider.DoesCollideWithGround = true;
                        }

                        Shaker.ShakeX();
                    }
                }

                if (IsGodMode && !IsMovingBetweenScenes)
                {
                    Liver.Active = false;
                    HurtHandler.Active = false;
                    Collider.DoesCollideWithGround = false;
                    CrushChecker.Active = false;
                    Sprite.Rotation += 5f * Time.Delta;
                    Mover.Velocity = Vector2.Zero;
                    if (MyGame.Cycle % 10 == 0) Sprite.Color = Paint.Rainbow.Where(p => p != Sprite.Color).Random();

                    Vector2 gDir = new Vector2(0, 0);
                    if (Controller.Left.IsHeld) gDir.X--;
                    if (Controller.Right.IsHeld) gDir.X++;
                    if (Controller.Up.IsHeld) gDir.Y--;
                    if (Controller.Down.IsHeld) gDir.Y++;
                    Transform.Position += gDir * 150 * (IsMoonwalking ? 1f : 2f) * Time.Delta;
                    if (identity == "PlayerOne" && OtherPlayer?.IsAvailable == true)
                    {
                        OtherPlayer.Transform.Position = Transform.Position;
                    }
                    return;
                }
            }

            changedGravityTime -= Time.ModifiedDelta;

            blinkTime -= Time.Delta;
            if (blinkTime <= 0)
            {
                blink = !blink;
                blinkTime = blinkDuration;
            }

            // if (MyGame.CurrentZone == "Title") CreateRecording();

            UpdateInput();

            CheckCostume();
            CheckBreakBadge();
            CheckRun();

            if (IsStupid)
            {
                if (Controller.SpecialOne.IsPressed || Controller.SpecialTwo.IsPressed)
                {
                    Nope();
                }

                if (!MyGame.IsTalking) StupidTime -= Time.ModifiedDelta;
                stupidEffectsTime -= Time.Delta;

                if (stupidEffectsTime <= 0)
                {
                    List<Color> colors = new List<Color>() { Paint.Red, Paint.Blue, Paint.Green, Paint.Yellow };
                    Factory.ParticleEffect(texture: Shapes.StringAsTexture(Lang.Font, "?", colors.Random(), Paint.Black), position: Transform.Position);
                    Factory.SfxPlayer(name: "IceSlip", pitch: Chance.Range(-0.75f, -0.25f), position: Transform.Position, volume: 0.5f);
                    Shaker.ShakeX();
                    stupidEffectsTime = stupidEffectsDuration;
                    TintOverride = colors.Where(t => t != TintOverride).Random();
                }

                if (StupidTime <= 0)
                {
                    UnscrambleControls();
                    RecoilStart(duration: 0.9f);
                }
            }

            // if (!canGo)
            // {
            //     if (MyGame.GetEntityWithTag("Fade")?.IsAvailable != true)
            //     {
            //         canGo = true;
            //     }
            // }

            // if (!canGo) return;

            hairballTime -= Time.ModifiedDelta;
            barkTime -= Time.ModifiedDelta;
            barkIntervalTime -= Time.ModifiedDelta;
            homingWadTime -= Time.ModifiedDelta;
            focusRoutineTimer -= Time.ModifiedDelta;
            runDustTime -= Time.ModifiedDelta;
            runSpriteTime -= Time.ModifiedDelta;
            breakTime -= Time.ModifiedDelta;
            elapsed += Time.ModifiedDelta;

            if (focusRoutine != null)
            {
                FocusSceneRoutine();
            }

            if (Freeze)
            {
                RefreshColor();
                Collider.DoesCollideWithGround = false;
                // Animator.Offset = 0;
                // Animator.SetState(new AnimationState("Freeze", 48));
                return;
            }
            else
            {
                if (!targetAbsolute && !IsGodMode && Liver.Health > 0 && State != "Launch" && State != "Transport")
                {
                    Collider.DoesCollideWithGround = true;
                }
            }

            webbedTime -= Time.ModifiedDelta;
            if (spitTime > spitDuration) spitTime = spitDuration;
            spitTime -= Time.ModifiedDelta;
            pauseTime -= Time.ModifiedDelta;

            if (Collider.DoesCollideWithGround)
            {
                KeepWithinBounds();
                // KeepWithinCamera();
            }

            if (IsTouchingWater)
            {
                if (no != null) HurtHandler.DoNo(no);
            }
            else
            {
                if (MyGame.CurrentZone != "SnowBase" &&  State != "Command" && ((!targetAbsolute && State != "Recoil" && isFallingOffScreen) || no != null))
                {
                    HurtHandler.DoNo(no);
                }
            }
            
            if (BadgeIsPressed(Badge.Break))
            {
                if (CanUseBreakBadge && new List<string>() { "Normal", "Swim", "Leap", "Bounce", "Dash", "PingPong" }.Contains(State))
                {
                    spitTime = 0.5f;

                    breakBehavior?.Trigger();
                    breakTime = breakDuration;
                    Factory.SfxPlayer(name: "Break", volume: 0.3f);
                    Camera.Shake(1f);
                    Factory.Sprite(
                        scene: Entity.Scene,
                        position: Transform.Position,
                        texture: pixel,
                        order: -20f,
                        spriteOrigin: SpriteOrigin.Center,
                        size: new Vector2(80, 80),
                        color: Paint.White * 0f
                    ).AddComponent(new Activity(a =>
                    {
                        Sprite sprite = a.Entity.GetComponent<Sprite>();
                        sprite.Scale += new Vector2(700f, 700f) * Time.ModifiedDelta;
                        float factor = (1 - (sprite.Scale.X / 350f));
                        if (factor <= 0)
                        {
                            a.Entity.Destroy();
                        }
                        else
                        {
                            sprite.Color = Color.White * factor * 0.2f;
                        }
                    }));

                    // MyGame.GetComponents<TimedBlockButtonBehavior>().ForEach(x =>
                    // {
                    //     if (!x.Entity.Tags.Contains("Player"))
                    //     {
                    //         Collider collider = x.Entity.GetComponent<Collider>();
                    //         if (collider != null && collider.IsOnScreen())
                    //         {
                    //             x.Activate();
                    //         }
                    //     }
                    // });

                    if (hairballInstance?.IsAvailable == true) hairballInstance.Entity.GetComponent<Mover>().Velocity = Vector2.Zero;

                    List<string> denyList = new List<string>()
                    {
                        "Player",
                        "TargetTrigger",
                        "BalloonPlatformHurtbox",
                        "IgnoreBreak",
                        "CoinBlock",
                    };

                    MyGame.GetComponentsWithTag<Mover>("Enemy").ConcatRange(MyGame.GetComponentsWithTag<Mover>("EnemyCollectible")).ConcatRange(MyGame.GetComponentsWithTag<Mover>("Boss")).ForEach(e =>
                    {
                        if (!denyList.Any(d => e.Entity.Tags.Contains(d)))
                        {
                            e.Velocity = Vector2.Zero;
                            Shaker s = e.Entity.GetComponent<Shaker>();
                            if (s != null) s.ShakeX(5, 0.95f);
                            Transform t = e.Entity.GetComponent<Transform>();
                            if (t != null)
                            {
                                Factory.DustParticleEffect(position: t.Position, size: new Vector2(8), amount: 6, order: -10);
                                Factory.SfxPlayer(name: "Bump", pitch: -0.5f, position: t.Position);
                            }

                            BreakReaction breakReaction = e.Entity.GetComponent<BreakReaction>();
                            if (breakReaction != null) breakReaction.Execute();
                        }
                    });

                    denyList.Add("Indestructible");

                    MyGame.GetComponents<Liver>().Where(l => l.Active).ToList().ForEach(x =>
                    {
                        if (!denyList.Any(d => x.Entity.Tags.Contains(d)))
                        {
                            Collider collider = x.Entity.GetComponent<Collider>();
                            if (collider != null && collider.IsOnScreen())
                            {
                                x.LoseHealth(SpitStrength);
                            }
                        }
                    });
                }
                else if (breakTime <= 0)
                {
                    Factory.SfxPlayer(name: "NuUh", volume: 1f, pitch: Chance.Range(0, 0.25f));
                    Factory.FadingText(position: Transform.Top + new Vector2(0, -8) + Utility.Shake(8), text: Dial.Key("NOPE"), color: Paint.Red, outlineColor: Paint.Black, duration: 0.5f);
                    breakBehavior?.Entity?.GetComponent<Shaker>()?.ShakeY();
                }
            }
            

            if (pauseTime > 0) return;

            if ((GravityChanger.Name == "Down" && Collider.Info.DownTags.Contains("Ice"))
                || (GravityChanger.Name == "Up" && Collider.Info.UpTags.Contains("Ice"))
                || (GravityChanger.Name == "Right" && Collider.Info.RightTags.Contains("Ice"))
                || (GravityChanger.Name == "Left" && Collider.Info.LeftTags.Contains("Ice"))
            )
            {
                isOnIce = true;
            }
            else
            {
                isOnIce = false;
            }

            maxFallSpeed = CONSTANTS.MAX_FALL_SPEED;
            minJumpSpeed = MIN_JUMP_SPEED;

            IsTouchingAntiGravity = Collider.IsTouchingAntiGravity();
            IsTouchingWater = Collider.IsTouchingWater();

            if (IsTouchingAntiGravity || IsTouchingWater) fellOffScreenCount = 1;
            
            // Gravity
            if (IsTouchingAntiGravity)
            {
                gravity = CONSTANTS.GRAVITY / 2f;
                maxFallSpeed = CONSTANTS.MAX_FALL_SPEED / 5f;
                if (Controller.Jump.IsHeld && Mover.Velocity.V_Lesser(-maxFallSpeed))
                {
                    Mover.Velocity.V(-maxFallSpeed * 2f);
                }
                if (Mover.Velocity.V_Greater(0) && !IsFeetColliding)
                {
                    maxFallSpeed = CONSTANTS.MAX_FALL_SPEED / 8f;
                }

                bool fastFall = false;
                if (GravityChanger.Direction.Y == 1 && Controller.Down.IsHeld) fastFall = true;
                else if (GravityChanger.Direction.Y == -1 && Controller.Up.IsHeld) fastFall = true;
                else if (GravityChanger.Direction.X == 1 && Controller.Right.IsHeld) fastFall = true;
                else if (GravityChanger.Direction.X == -1 && Controller.Left.IsHeld) fastFall = true;

                if (!Controller.Jump.IsHeld) maxFallSpeed = CONSTANTS.MAX_FALL_SPEED / 3.5f;
                if (State == "Bounce" && fastFall && Mover.Velocity.V_Greater(0)) maxFallSpeed = CONSTANTS.MAX_FALL_SPEED / 2f;
                minJumpSpeed = MIN_JUMP_SPEED / 3f;

                // ENTER
                if (!JustExitedAntiGravity)
                {
                    Factory.SfxPlayer(name: "WaterEnter", pitch: -1f, position: Transform.Position);
                    Factory.ParticleEffect(
                        position: Transform.Position,
                        spriteTemplate: new Sprite(pixel, scale: new Vector2(3, 3), color: Paint.White),
                        fadeOverTime: true,
                        amount: 4
                    );
                    Shaker.ShakeX(2f);
                    if (Controller.Jump.IsHeld && Mover.Velocity.V_Lesser(CONSTANTS.MAX_FALL_SPEED * 0.25f))
                    {
                        Mover.Velocity.V(-maxFallSpeed * 2);
                    }
                    // IsFluttering = false;
                }
            }
            else
            {
                // EXIT
                if (JustExitedAntiGravity)
                {
                    if (Controller.Jump.IsHeld && !Collider.IsTouchingAntiGravity(Transform.Position - (GravityChanger.Direction * (Transform.Height + 4f))))
                    {
                        Mover.Velocity.V(minJumpSpeed);
                        Factory.SfxPlayer(name: "WaterExit", pitch: -0.35f, position: Transform.Position);
                        Factory.ParticleEffect(
                            position: Transform.Position,
                            spriteTemplate: new Sprite(pixel, scale: new Vector2(3, 3), color: Paint.White),
                            fadeOverTime: true,
                            amount: 4
                        );
                        Shaker.ShakeY(2f);
                    }
                    else
                    {
                        Factory.SfxPlayer(name: "WaterExit", pitch: -1f, position: Transform.Position);
                        Factory.ParticleEffect(
                            position: Transform.Position,
                            spriteTemplate: new Sprite(pixel, scale: new Vector2(3, 3), color: Paint.White),
                            fadeOverTime: true,
                            amount: 4
                        );
                        Shaker.ShakeX(2f);
                    }

                    JustExitedAntiGravity = false;
                }

                gravity = CONSTANTS.GRAVITY;
            }


            // Bump Off Walls
            // Vertical
            if (GravityChanger.Direction.Y != 0)
            {
                if (IsTouchingAntiGravity)
                {
                    // if (Collider.Info.Left && Mover.Velocity.X < 0) Mover.Velocity.X *= -0.25f;
                    // if (Collider.Info.Right && Mover.Velocity.X > 0) Mover.Velocity.X *= -0.25f;
                }
                else if (!IsTouchingWater && State != "Leap")
                {
                    if (Collider.Info.Left || Collider.Info.Right)
                    {
                        if (!fellOffScreenCanRecover)
                        {
                            Mover.Velocity.X *= -0.75f;
                            Factory.SfxPlayer(name: "Bump", position: Transform.Position);
                            Factory.DustParticleEffect(position: Transform.Position);
                        }
                        else if (((Collider.Info.Left && input.X != -1 && Mover.Velocity.X < 0) 
                            || (Collider.Info.Right && input.X != 1 && Mover.Velocity.X > 0)))
                        {
                            Mover.Velocity.X = 0.1f * Mover.Velocity.X.Sign();
                        }
                    }
                }
            }
            // Horizontal
            else
            {
                if (IsTouchingAntiGravity)
                {
                    // ...
                }
                else if (!IsTouchingWater && State != "Leap")
                {
                    if (Collider.Info.Up || Collider.Info.Down)
                    {
                        if (!fellOffScreenCanRecover)
                        {
                            Mover.Velocity.Y *= -0.75f;
                            Factory.SfxPlayer(name: "Bump", position: Transform.Position);
                            Factory.DustParticleEffect(position: Transform.Position);
                        }
                        else if (((Collider.Info.Up && input.Y != -1 && Mover.Velocity.Y < 0) 
                            || (Collider.Info.Down && input.Y != 1 && Mover.Velocity.Y > 0)))
                        {
                            Mover.Velocity.Y = 0.1f * Mover.Velocity.Y.Sign();
                        }
                    }
                }
            }

            switch (State)
            {
                default:
                case "Normal":
                    Normal();
                    break;

                case "Swim":
                    Swim();
                    break;

                case "Recoil":
                    Recoil();
                    break;

                case "Leap":
                    Leap();
                    break;

                case "Dash":
                    Dash();
                    break;

                case "Talk":
                    Talk();
                    break;

                case "Target":
                    Target();
                    break;

                case "Stand":
                    Stand();
                    break;

                case "Launch":
                    Launch();
                    break;

                case "Win":
                    Win();
                    break;

                case "Ball":
                    Ball();
                    break;

                case "Lerp":
                    Lerp();
                    break;

                case "Stop":
                    Stop();
                    break;

                case "Bounce":
                    Bounce();
                    break;

                case "PingPong":
                    PingPong();
                    break;

                case "Bubble":
                    Bubble();
                    break;

                case "Parry":
                    Parry();
                    break;

                case "Transport":
                    Transport();
                    break;

                case "Command":
                    break;
            }

            CheckRopePoints();
            CheckTreadmills();
            CheckOtherPlayer();
            CheckDistanceToOtherPlayer();
            CheckHealthShake();
            // if (Pusher?.IsAvailable == true) Pusher.Active = SaveData.Current?.FriendlyFire == true;

            if (IsTouchingAntiGravity)
            {
                JustExitedAntiGravity = true;
            }

            // Checkpoint

            // Going down.
            if (queueDownCheckpoint)
            {
                SetCheckpointInNewScene();
            }
            // Going up, left, or right.
            else if (queueCheckpoint)
            {
                // Swimming
                if (IsTouchingWater)
                {
                    SetCheckpointInNewScene();
                }
                // Find first collision below me
                else if (MyGame.Cycle % 10 == 0)
                {
                    Vector2? hitLeft = Collider.GetHitInDirection(
                        standardBottomLeft,
                        GravityChanger.Direction,
                        2,
                        new List<string>() { "Collision" },
                        new List<string>() { "Transporter", "Crumbly", "No" },
                        4
                    );

                    Vector2? hitRight = Collider.GetHitInDirection(
                        standardBottomRight,
                        GravityChanger.Direction,
                        2,
                        new List<string>() { "Collision" },
                        new List<string>() { "Transporter", "Crumbly", "No" },
                        4
                    );

                    Vector2? hit = null;

                    if (hitLeft != null && hitRight != null) hit = new Vector2(Transform.Position.X, MathF.Min(hitLeft.Value.Y, hitRight.Value.Y));
                    else if (hitLeft != null) hit = hitLeft;
                    else if (hitRight != null) hit = hitRight;

                    if (hit != null)
                    {
                        SetCheckpointInNewScene(new Vector2(
                            hit.Value.X,
                            hit.Value.Y.ToNearest(16) - (Transform.Height / 2f)
                        ));
                    }
                }
            }

            // ---

            doorCooldownTime -= Time.ModifiedDelta;

            if (MyGame.IsTalking)
            {
                HideHealthUi();
            }
            else
            {
                ShowHealthUi();
            }

            // Clamp
            if (State != "Launch")
            {
                if (GravityChanger.Direction.Y == 1)
                {
                    if (Mover.Velocity.Y > maxFallSpeed) Mover.Velocity.Y = maxFallSpeed;
                    if (Mover.Velocity.Y < -maxFallSpeed * 2) Mover.Velocity.Y = -maxFallSpeed * 2;
                }
                else if (GravityChanger.Direction.Y == -1)
                {
                    if (Mover.Velocity.Y < -maxFallSpeed) Mover.Velocity.Y = -maxFallSpeed;
                    if (Mover.Velocity.Y > maxFallSpeed * 2) Mover.Velocity.Y = maxFallSpeed * 2;
                }
                else if (GravityChanger.Direction.X == 1)
                {
                    if (Mover.Velocity.X > maxFallSpeed) Mover.Velocity.X = maxFallSpeed;
                    if (Mover.Velocity.X < -maxFallSpeed * 2) Mover.Velocity.X = -maxFallSpeed * 2;
                }
                else if (GravityChanger.Direction.X == -1)
                {
                    if (Mover.Velocity.X < -maxFallSpeed) Mover.Velocity.X = -maxFallSpeed;
                    if (Mover.Velocity.X > maxFallSpeed * 2) Mover.Velocity.X = maxFallSpeed * 2;
                }
            }

            if (IsFeetColliding)
            {
                JustTransitionedScenes = false;
                didForceJump = false;
                dampenGravity = false;
                didJumpFromWater = false;
            }

            if (animationState != null) Animator.SetState(animationState);

            RefreshColor();

            // StaminaBar.Visible = false;

            // Stamina
            // Transform staminaPickup = Collider.GetCollision("Stamina");
            // if (spitTime <= 0) stamina += staminaRegen * Time.Delta;
            // if (stamina >= staminaMax || staminaPickup != null)
            // {
            //     if (staminaPickup != null)
            //     {
            //         staminaPickup.Entity.Destroy();
            //         Factory.SfxPlayer(Entity.Scene, "Jump", pitch: 0.5f);
            //     }
            //     staminaDisplayTime += Time.Delta;
            //     stamina = staminaMax;
            //     waitForStamina = false;
            //     StaminaBar.Color = Paint.HexToColor("#61a53f");
            //     StaminaBar.BackgroundColor = Paint.HexToColor("#000000");
            // }
            // StaminaBar.Max = staminaMax;
            // StaminaBar.Current = stamina;
            // if (staminaDisplayTime >= 1)
            // {
            //     StaminaBar.Visible = false;
            // }
            // else
            // {
            //     StaminaBar.Visible = true;
            // }

            PlayerCornerChecker.Reset();
        }

        public override void LateUpdate()
        {
            base.LateUpdate();

            Entity.RemoveTag("JustMoved");
            if (Scooched)
            {
                gravityGraceTime = 0;
                didJump = true;
                Scooched = false;
            }

            CheckFocusScene();
            // KeepWithinCamera();

            if (controllerOverride != null) controllerOverride.LateUpdate();
        }

        void UpdateInput()
        {
            quickSwapLockInputCooldown -= Time.Delta;

            if (quickSwapLockInput.X == -1 && (!Controller.Left.IsHeld || quickSwapLockInputCooldown <= 0)) quickSwapLockInput.X = 0;
            if (quickSwapLockInput.X == 1 && (!Controller.Right.IsHeld || quickSwapLockInputCooldown <= 0)) quickSwapLockInput.X = 0;
            if (quickSwapLockInput.Y == -1 && (!Controller.Up.IsHeld || quickSwapLockInputCooldown <= 0)) quickSwapLockInput.Y = 0;
            if (quickSwapLockInput.Y == 1 && (!Controller.Down.IsHeld || quickSwapLockInputCooldown <= 0)) quickSwapLockInput.Y = 0;

            input = new Vector2();
            if (Controller.Left.IsHeld && (QuickSwap.IsEnabled || quickSwapLockInput.X != -1)) input.X -= 1;
            if (Controller.Right.IsHeld && (QuickSwap.IsEnabled || quickSwapLockInput.X != 1)) input.X += 1;
            if (Controller.Up.IsHeld && (QuickSwap.IsEnabled || quickSwapLockInput.Y != -1)) input.Y -= 1;
            if (Controller.Down.IsHeld && (QuickSwap.IsEnabled || quickSwapLockInput.Y != 1)) input.Y += 1;
            if (QuickSwap.IsEnabled)
            {
                quickSwapLockInput = input;
                quickSwapLockInputCooldown = 0.15f;
                input = Vector2.Zero;
            }

            if (!IsInControl) input = Vector2.Zero;

            if (input.X == 0) didReleaseInputX = true;
            if (input.Y == 0) didReleaseInputY = true;
        }

        // Recording

        InputOverride inputOverrideJump = null;
        InputOverride inputOverrideSpit = null;
        InputOverride inputOverrideLeft = null;
        InputOverride inputOverrideRight = null;
        InputOverride inputOverrideUp = null;
        InputOverride inputOverrideDown = null;

        void CreateRecording()
        {
            if (Input.IsPressed("R"))
            {
                Debug.WriteLine("...");
                inputOverrides.ForEach(i =>
                {
                    Debug.WriteLine($"new InputOverride(\"{i.Name}\", {i.Time}f, {i.Duration}f)");
                });
                Debug.WriteLine("...");
            }

            float zoneTime = StatListener.Time;

            // Jump
            if (Input.Controllers.Any(c => c.Jump.IsPressed)) inputOverrideJump = new InputOverride("Jump", zoneTime, 0);
            if (Input.Controllers.Any(c => c.Jump.IsReleased))
            {
                inputOverrideJump.Duration = zoneTime - inputOverrideJump.Time;
                inputOverrides.Add(inputOverrideJump);
                inputOverrideJump = null;
            }

            // Spit
            if (Input.Controllers.Any(c => c.Spit.IsPressed)) inputOverrideSpit = new InputOverride("Spit", zoneTime, 0);
            if (Input.Controllers.Any(c => c.Spit.IsReleased))
            {
                inputOverrideSpit.Duration = zoneTime - inputOverrideSpit.Time;
                inputOverrides.Add(inputOverrideSpit);
                inputOverrideSpit = null;
            }

            // Left
            if (Input.Controllers.Any(c => c.Left.IsPressed)) inputOverrideLeft = new InputOverride("Left", zoneTime, 0);
            if (Input.Controllers.Any(c => c.Left.IsReleased))
            {
                inputOverrideLeft.Duration = zoneTime - inputOverrideLeft.Time;
                inputOverrides.Add(inputOverrideLeft);
                inputOverrideLeft = null;
            }

            // Right
            if (Input.Controllers.Any(c => c.Right.IsPressed)) inputOverrideRight = new InputOverride("Right", zoneTime, 0);
            if (Input.Controllers.Any(c => c.Right.IsReleased))
            {
                inputOverrideRight.Duration = zoneTime - inputOverrideRight.Time;
                inputOverrides.Add(inputOverrideRight);
                inputOverrideRight = null;
            }

            // Up
            if (Input.Controllers.Any(c => c.Up.IsPressed)) inputOverrideUp = new InputOverride("Up", zoneTime, 0);
            if (Input.Controllers.Any(c => c.Up.IsReleased))
            {
                inputOverrideUp.Duration = zoneTime - inputOverrideUp.Time;
                inputOverrides.Add(inputOverrideUp);
                inputOverrideUp = null;
            }

            // Down
            if (Input.Controllers.Any(c => c.Down.IsPressed)) inputOverrideDown = new InputOverride("Down", zoneTime, 0);
            if (Input.Controllers.Any(c => c.Down.IsReleased))
            {
                inputOverrideDown.Duration = zoneTime - inputOverrideDown.Time;
                inputOverrides.Add(inputOverrideDown);
                inputOverrideDown = null;
            }
        }

        public void UnscrambleControls()
        {
            if (!IsStupid) return;

            HurtHandler.TriggerInvincibility(2f);

            Shaker.ShakeY();
            // Factory.ParticleEffect(texture: Shapes.StringAsTexture(Lang.Font, "!", Paint.Orange, Paint.Black), position: Transform.Position);
            Factory.ParticleEffectSpecial(texture: Shapes.StringAsTexture(Lang.Font, Dial.Key("GENIUS"), Paint.White, Paint.Black), position: Transform.Position + new Vector2(-24, -16), size: new Vector2(1, 1), speed: new Range(0, 0));
            Factory.DustParticleEffect();
            Factory.SfxPlayer(name: "Notice", position: Transform.Position, pitch: 1f);

            StupidTime = 0;
            IsStupid = false;
            controllerOverride = null;
            // CheckCostume(true);

            TintOverride = null;
        }

        public void ScrambleControls()
        {
            StupidTime = stupidDuration;

            Factory.SfxPlayer(name: "GhostLaugh", pitch: 0.5f);

            HurtHandler.TriggerInvincibility(2);

            List<Color> colors = new List<Color>() { Paint.Red, Paint.Orange, Paint.Yellow };
            Factory.ParticleEffectSpecial(texture: Shapes.StringAsTexture(Lang.Font, Dial.Key("STUPID"), colors.Random(), Paint.Black), position: Transform.Position + new Vector2(24, -16), size: new Vector2(1, 1), speed: new Range(0, 0));
            TintOverride = colors.Where(t => t != TintOverride).Random();

            if (IsStupid) return;

            IsStupid = true;

            controllerOverride = Utility.Duplicate(PlayerProfile.Controller);

            controllerOverride.Left.Inputs = PlayerProfile.Controller.Right.Inputs;
            controllerOverride.Right.Inputs = PlayerProfile.Controller.Left.Inputs;
            controllerOverride.Up.Inputs = PlayerProfile.Controller.Down.Inputs;
            controllerOverride.Down.Inputs = PlayerProfile.Controller.Up.Inputs;
        }

        void PlayRecording()
        {
            controllerOverride = new VirtualController() { };
            controllerOverride.Jump = new VirtualButton("Jump", controllerOverride, pressedOverride: () => jumpOverride, heldOverride: () => jumpOverride);
            controllerOverride.Spit = new VirtualButton("Spit", controllerOverride, pressedOverride: () => spitOverride, heldOverride: () => spitOverride);
            controllerOverride.Moonwalk = new VirtualButton("Moonwalk", controllerOverride, pressedOverride: () => moonwalkOverride, heldOverride: () => moonwalkOverride);
            controllerOverride.SpecialOne = new VirtualButton("SpecialOne", controllerOverride, pressedOverride: () => false);
            controllerOverride.SpecialTwo = new VirtualButton("SpecialTwo", controllerOverride, pressedOverride: () => false);
            controllerOverride.Start = new VirtualButton("Start", controllerOverride, pressedOverride: () => false);
            controllerOverride.Up = new VirtualButton("Up", controllerOverride, pressedOverride: () => upOverride, heldOverride: () => upOverride);
            controllerOverride.Down = new VirtualButton("Down", controllerOverride, pressedOverride: () => downOverride, heldOverride: () => downOverride);
            controllerOverride.Left = new VirtualButton("Left", controllerOverride, pressedOverride: () => leftOverride, heldOverride: () => leftOverride);
            controllerOverride.Right = new VirtualButton("Right", controllerOverride, pressedOverride: () => rightOverride, heldOverride: () => rightOverride);

            float inputOverrideTime = 0;
            if (Entity.Tags.Contains("PlayerTwo")) inputOverrideTime = -1;

            Routine.New(r =>
            {
                jumpOverride = false;
                spitOverride = false;
                moonwalkOverride = false;
                leftOverride = false;
                rightOverride = false;
                downOverride = false;
                upOverride = false;

                var currentInputOverrides = inputOverrides.Where(i => i.Time <= inputOverrideTime && i.Time + i.Duration >= inputOverrideTime).ToList();

                foreach (var inputOverride in currentInputOverrides)
                {
                    if (inputOverride.Name == "Jump") jumpOverride = true;
                    else if (inputOverride.Name == "Spit") spitOverride = true;
                    else if (inputOverride.Name == "Left") leftOverride = true;
                    else if (inputOverride.Name == "Right") rightOverride = true;
                    else if (inputOverride.Name == "Down") downOverride = true;
                    else if (inputOverride.Name == "Up") upOverride = true;
                }

                inputOverrideTime += Time.ModifiedDelta;

                if (inputOverrideTime >= inputOverrides.Last().Time + inputOverrides.Last().Duration + 1f && !Fade.IsFading)
                {
                    Factory.Fade(() =>
                    {
                        Place.LoadZone("Title");
                    });
                }

                return false;
            });
        }

        // Utilities

        public void KeepWithinBounds()
        {
            if (GravityChanger.IsVertical || IsTouchingWater)
            {
                if (Transform.Position.X < MyGame.CurrentScene.CameraLeft.X + (Transform.Width / 2f)) Transform.Position.X = MyGame.CurrentScene.CameraLeft.X + (Transform.Width / 2f);
                if (Transform.Position.X > MyGame.CurrentScene.CameraRight.X - (Transform.Width / 2f)) Transform.Position.X = MyGame.CurrentScene.CameraRight.X - (Transform.Width / 2f);
            }

            if (GravityChanger.IsHorizontal || IsTouchingWater)
            {
                if (Transform.Position.Y < MyGame.CurrentScene.CameraTop.Y + (Transform.Height / 2f)) Transform.Position.Y = MyGame.CurrentScene.CameraTop.Y + (Transform.Height / 2f);
                if (Transform.Position.Y > MyGame.CurrentScene.CameraBottom.Y - (Transform.Height / 2f)) Transform.Position.Y = MyGame.CurrentScene.CameraBottom.Y - (Transform.Height / 2f);
            }
        }

        public void KeepWithinCamera()
        {
            if (Transform.Position.Y > Camera.WorldBottom.Y - (Transform.Height / 2f))
            {
                Transform.Position.Y = Camera.WorldBottom.Y - (Transform.Height / 2f);
                Collider.Info.BakedDown = true;
                TouchingCamera = true;
            }
        }

        public void SetLastSafePosition(Vector2 position)
        {
            if (lastSafePosition?.IsAvailable == true)
            {
                lastSafePosition.Position = position - new Vector2(0, lastSafePosition.Height / 2f);
            }
            else
            {
                Entity en = new Entity("LastSafePosition", Entity.Scene);
                en.AddTag("Passenger");
                en.AddTag("Invisible");
                en.AddTag("IgnoreTouch");
                lastSafePosition = en.AddComponent<Transform>();
                lastSafePosition.ColorBounds = Paint.Transparent;
                lastSafePosition.ColorGizmo = Paint.Pink;
                lastSafePosition.Width = Transform.Width;
                lastSafePosition.Height = Transform.Height;
                lastSafePosition.Position = position - new Vector2(0, lastSafePosition.Height / 2f);

                Mover mover = en.AddComponent<Mover>();
                mover.Velocity = new Vector2(0, 0.1f);
                Collider collider = en.AddComponent<Collider>();
            }
        }

        void CheckPhaseThrough()
        {
            Vector2 offset = new Vector2(0, -(16 + Transform.Height + 1));
            if (GravityChanger.Direction.Y == -1) offset.Y *= -1;
            if (GravityChanger.Direction.X == -1)
            {
                offset.X = (16 + Transform.Width + 1);
                offset.Y = 0;
            }
            if (GravityChanger.Direction.X == 1)
            {
                offset.X = -(16 + Transform.Width + 1);
                offset.Y = 0;
            }

            if (Camera.IsPointInside(Transform.Position + offset) && !Collider.IsTouching(Transform.Corners.Select(p => p += offset).ToList()))
            {
                if (GravityChanger.IsVertical) Mover.Velocity.Y *= 0.5f;
                else Mover.Velocity.X *= 0.5f;
                Transform.Position += offset;

                Factory.SfxPlayer(name: "Jump", pitch: 1f, position: Transform.Position);
                Shaker.ShakeX();
                Factory.DustParticleEffect(position: Transform.Position, color: Paint.Pink);
            }
            else
            {
                if (GravityChanger.IsVertical) Mover.Velocity.Y *= -0.75f;
                else Mover.Velocity.X *= -0.75f;
            }
        }

        void DestroyCrumblies()
        {
            List<Vector2> clearPoints = new List<Vector2>()
            {
                Transform.Position
            };

            // Destroy crumblies
            if (!MyGame.IsTalking && (PlayerProfile.Index != 2 || OtherPlayer?.Entity.IsAvailable != true))
            {
                MyGame.CurrentScene.GetComponents<Crumbly>()
                    .Where(c => clearPoints.Any(p => p.Distance(c.Transform.Position) < 48))
                    .ToList()
                    .ForEach(c =>
                    {
                        c.Entity.Destroy();
                        if (c.Auto) c.UpdateNeighbors();
                    });

                MyGame.CurrentScene.GetComponentsWithTag<Transform>("SpinningBlock")
                   .Where(c => clearPoints.Any(p => p.Distance(c.Position) < 48))
                   .ToList()
                   .ForEach(c =>
                   {
                       c.Entity.GetComponent<SpinningBlock>().Activate();
                   });
            }
        }

        public void FinishTeleport()
        {
            if (JustTeleported != null)
            {
                JustTeleported.Invoke();
                if (breakBehavior?.IsAvailable == true) breakBehavior.Transform.Position = Transform.Position;
            }
        }

        void Scooch()
        {
            Scooched = true;

            int max = 40;
            Vector2 offset = Vector2.Zero;
            List<Vector2> directions = new List<Vector2>() { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) };
            Vector2? safePostion = null;
            int score = max;
            for (int d = 0; d < directions.Count; d++)
            {
                for (int i = 0; i < max; i++)
                {
                    List<Vector2> points = Transform.Corners.Select(c => c + offset).ToList();
                    if (!Collider.IsTouching(points) && points.All(p => MyGame.CurrentScene.IsPointInside(p)))
                    {
                        if (i < score)
                        {
                            safePostion = Transform.Position + offset;
                            score = i;
                        }
                    }

                    offset += directions[d] * 2;
                }

                offset = Vector2.Zero;
            }

            if (safePostion != null)
            {
                Transform.Position = safePostion.Value;
                Entity.AddTag("JustMoved");
                if (Chance.CoinFlip()) Shaker.ShakeX();
                else Shaker.ShakeY();
                Factory.SfxPlayer(name: "Jump", pitch: 1f, position: Transform.Position);
                Factory.DustParticleEffect(position: Transform.Position, color: Paint.Pink);
            }
            else
            {
                Liver.LoseHealth(69420);
            }
        }

        void SetCheckpointInNewScene(Vector2? position = null)
        {
            if (newScene != null && newScene.GetEntityWithTag("NoCheckpoint") != null) return;

            if (Collider.IsTouching()) return;

            Place.Checkpoint(
                position: position ?? Transform.Position,
                flip: Sprite.FlipHorizontally,
                scene: newScene
            );
            queueCheckpoint = false;
            queueDownCheckpoint = false;
            if (OtherPlayer?.IsAvailable == true)
            {
                OtherPlayer.queueCheckpoint = false;
                OtherPlayer.queueDownCheckpoint = false;
            }
        }

        void CheckBreakBadge()
        {
            if (Liver.Health <= 0) return;

            if (Badge.Break.IsUnlocked)
            {
                if (breakBehavior?.IsAvailable != true)
                {
                    breakBehavior = Factory.Break(Entity.Scene, this).GetComponent<BreakBehavior>();
                }

                if (breakBehavior?.IsAvailable == true)
                {   
                    if (BadgeIsEquipped(Badge.Break) || Controller?.InputProfile?.BadgeStyle == "Expert")
                    {
                        breakBehavior.Show = true;
                    }
                    else
                    {
                        breakBehavior.Show = false;
                    }
                }
            }
            else
            {
                if (breakBehavior?.IsAvailable == true)
                {
                    breakBehavior.Entity.Destroy();
                }
            }
        }

        void CheckRun()
        {
            if (!disableRunEffects && (IsRunning || lockRunEffects) && runSpriteTime <= 0)
            {
                if (State == "Parry") runSpriteTime = runSpriteDuration * 0.25f;
                else runSpriteTime = runSpriteDuration;

                Factory.SpeedSprite(Entity.Scene, Transform.Position, Sprite, PlayerProfile.PlayerTextureGrayScale);
            }
        }

        public void SetDoorCooldown(float? duration = null)
        {
            didReleaseInputSinceDoor = false;
            doorCooldownTime = duration ?? 1f;
        }

        public void HideHealthUi()
        {
            if (ui?.IsAvailable != true) return;

            foreach (UiSprite sprite in heartsUi)
            {
                sprite.Entity.Visible = false;
            }

            ui.Visible = false;
        }

        public void ShowHealthUi()
        {
            if (ui?.IsAvailable != true) return;

            foreach (UiSprite sprite in heartsUi)
            {
                sprite.Entity.Visible = true;
            }

            ui.Visible = true;
        }

        void CheckRopePoints()
        {
            if (MyGame.PlayerCount == 2 && OtherPlayer?.IsAvailable == true && Entity.Tags.Contains("PlayerOne"))
            {
                ropePoints = new List<Vector2>() { };

                int segmentCount = 20;

                Vector2 difference = OtherPlayer.Transform.Position - Transform.Position;
                Vector2 range = difference / segmentCount;

                for (int i = 0; i <= segmentCount; i++)
                    ropePoints.Add(range * i);
            }
        }

        void CheckFocusScene()
        {
            if (!CanFocusScene || MyGame.IsTalking || State == "Bubble" || doorCooldownTime > 0) return;

            Scene newScene = MyGame.Scenes.Find(s =>
                !s.Remove
                    && !s.IsOmnipresent
                    && s != MyGame.CurrentScene
                    && s.IsRectangleInside(
                        Transform.TopLeft - new Vector2(4, 4),
                        Transform.BottomRight + new Vector2(4, 4)
                    )
            );

            if (newScene != null)
            {
                IsMovingBetweenScenes = true;
                this.newScene = newScene;
                focusRoutine = 0;
                CanFocusScene = false;
                if (OtherPlayer?.IsAvailable == true) OtherPlayer.CanFocusScene = false;
            }
        }

        public bool CheckReviveOtherPlayer(bool force = false, int? health = null)
        {
            if (SaveData.Current.CostumePlayerTwo == "Ghostly" || MyGame.PlayerCount <= 1) return false;
            
            if (QueueOtherPlayerRevive || force)
            {
                bool ghost = false;
                MyGame.GetComponents<PlayerGhost>().ForEach(p => 
                {
                    Factory.SfxPlayer(name: "HeartDrop", pitch: -0.5f);
                    Factory.HeartParticleEffect(position: p.Transform.Position);
                    p.State = "Revive";
                    ghost = true;
                });

                if (!ghost)
                {
                    ReviveOtherPlayer(health);
                    return true;
                }
            }

            return false;
        }

        public void ReviveOtherPlayer(int? health = null)
        {
            if (SaveData.Current.CostumePlayerTwo == "Ghostly" || MyGame.PlayerCount <= 1 || !QueueOtherPlayerRevive) return;
            if (OtherPlayer?.IsAvailable == true && OtherPlayer.Liver.Health <= 0) return;
            QueueOtherPlayerRevive = false;

            MyGame.GetComponents<PlayerGhost>().ForEach(p =>
            {
                p.Shatterer.Shatter();
                p.Entity.Destroy();
            });

            OtherPlayer = Factory.Player(
                scene: MyGame.PlayerContainer,
                position: Transform.Position,
                flip: Sprite.FlipHorizontally,
                tags: null,
                index: Entity.Tags.Contains("PlayerTwo") ? 1 : 2,
                signifier: Player.KeepSignifier
            )?.GetComponent<Player>();
            SkipCheckpoint = true;

            if (OtherPlayer.Liver.MaxHealth > 1) OtherPlayer.UiHealthBar.Popup(recover: true);
            Factory.OneShotTimer(end: _ =>
            {
                if (OtherPlayer?.IsAvailable == true) OtherPlayer.Mover.Velocity.Y += minJumpSpeed;
            }, duration: 0.01f);
            Factory.DustParticleEffect(position: OtherPlayer.Transform.Position);
            Factory.SfxPlayer(name: "Teleport", pitch: 0.5f);
            OtherPlayer.DidJumpFromSpring = true;
            OtherPlayer.HurtHandler.TriggerInvincibility(1.5f);
            if (health != null) OtherPlayer.Liver.Health = health.Value;
            if (MyGame.IsTalking || GarbanzoQuest.Talk.Letterbox?.IsAvailable == true) OtherPlayer.StandStart();
        }

        void FocusSceneRoutine()
        {
            if (focusRoutineTimer > 0) return;

            if (MyGame.IsTalking)
            {
                focusRoutine = null;
                return;
            }

            // Figure stuff out
            if (focusRoutine == 0)
            {
                if (GravityChanger.Name != "Down")
                {
                    Mover.Velocity = Vector2.Zero;
                    if (OtherPlayer?.Mover?.IsAvailable == true) OtherPlayer.Mover.Velocity = Vector2.Zero;
                    GravityChanger.ChangeGravity("Down");
                }

                Player.PlayerTwoJustDied = false;

                Camera.ClearBounds();

                oldScene = MyGame.CurrentScene;
                newScene.Active = true;

                // Figure out direction

                Vector2 direction = new Vector2(0, 0);

                // horizontal
                if ((newScene.Left.X <= MyGame.CurrentScene.Right.X) || (newScene.Right.X >= MyGame.CurrentScene.Left.X))
                {
                    // up
                    if (newScene.Right.X <= MyGame.CurrentScene.Left.X) direction.X = -1;
                    // down
                    if (newScene.Left.X >= MyGame.CurrentScene.Right.X) direction.X = 1;
                }
                // vertical
                if ((newScene.Top.Y <= MyGame.CurrentScene.Bottom.Y) || (newScene.Bottom.Y >= MyGame.CurrentScene.Top.Y))
                {
                    // up
                    if (newScene.Bottom.Y <= MyGame.CurrentScene.Top.Y) direction.Y = -1;
                    // down
                    if (newScene.Top.Y >= MyGame.CurrentScene.Bottom.Y) direction.Y = 1;
                }

                // Adjust me

                if (doorCooldownTime <= 0 && direction.Y == -1 && State != "Swim")
                {
                    didForceJump = true;
                    Mover.Velocity.Y = jumpSpeed;
                }

                playerDestination = Transform.Position + (direction * CONSTANTS.TILE_SIZE * 2f);
                otherPlayerDestination = Transform.Position + (direction * CONSTANTS.TILE_SIZE * 3f);

                CrushChecker.Active = false;
                Liver.Active = false;
                HurtHandler.Active = false;
                isBarking = false;
                barkCount = 0;

                // Adjust other player

                if (OtherPlayer?.IsAvailable == true)
                {
                    OtherPlayer.CrushChecker.Active = false;
                    OtherPlayer.Liver.Active = false;
                    OtherPlayer.HurtHandler.Active = false;
                    OtherPlayer.isBarking = false;
                    OtherPlayer.barkCount = 0;

                    // otherPlayer.Teleport(Transform.Position + (direction * CONSTANTS.HALF_TILE_SIZE));

                    if (doorCooldownTime <= 0 && direction.Y == -1 && State != "Swim")
                    {
                        OtherPlayer.didForceJump = true;

                        Mover otherMover = OtherPlayer.Entity.GetComponent<Mover>();
                        otherMover.Velocity.Y = jumpSpeed;
                    }
                }

                // Remove stuff that ventured outside of Scene

                MyGame.CurrentScene.GetComponents<Transform>().ForEach(t =>
                {
                    if (t.Entity.Name != "EvilFog" && MyGame.CurrentScene.IsTransformOutside(t))
                    {
                        t.Entity.Destroy();
                    }
                });

                // Reset switch

                Switch.Reset();

                // Show new scene

                MyGame.CurrentScene = newScene;
                newScene.Enter(MyGame.CurrentScene);
                newScene.Visible = true;

                // Destroy crumbly tiles if near player

                List<Vector2> clearPoints = new List<Vector2>()
                {
                    Transform.Position,
                    playerDestination
                };

                if (OtherPlayer?.IsAvailable == true)
                {
                    clearPoints.Add(OtherPlayer.Transform.Position);
                    clearPoints.Add(otherPlayerDestination);
                }

                newScene.GetComponents<Crumbly>().Where(c => clearPoints.Any(p => p.Distance(c.Transform.Position) < 48)).ToList().ForEach(c =>
                {
                    c.Entity.Destroy();
                    if (c.Auto) c.UpdateNeighbors();
                });

                if (PlayerChecker.ShowCoins)
                {
                    if (newScene?.GetComponents<Coin>()?.Count() <= 0) PlayerChecker.ShowCoins = false;
                }

                // Set checkpoint
                if (newScene.GetEntityWithTag("NoCheckpoint") == null)
                {
                    Transform forceCheckpoint = newScene
                        .GetComponentsWithTag<Transform>("ForceCheckpoint")
                        .OrderBy(x => x.Position.Distance(Transform.Position))
                        .FirstOrDefault();

                    if (forceCheckpoint != null)
                    {
                        queueCheckpoint = false;
                        queueDownCheckpoint = false;
                        Place.Checkpoint(position: forceCheckpoint.Position, scene: newScene);
                    }
                    // going down
                    else if (direction.Y == 1)
                    {
                        queueCheckpoint = false;
                        queueDownCheckpoint = true;
                    }
                    else
                    {
                        queueCheckpoint = true;
                        queueDownCheckpoint = false;
                    }
                }

                // Pan over

                Camera.Offset = Vector2.Zero;
                cameraTarget = MyGame.GetComponent<CameraTarget>();
                cameraTarget.State = "Command";
                cameraTarget.Transform.Position = Camera.WorldCenter;

                Freeze = true;
                DisableGravity = true;
                IsInControl = false;
                StoreVelocity = Mover.Velocity;
                Mover.Velocity = Vector2.Zero;
                if (OtherPlayer?.IsAvailable == true)
                {
                    OtherPlayer.Freeze = true;
                    OtherPlayer.DisableGravity = true;
                    OtherPlayer.IsInControl = false;
                    OtherPlayer.StoreVelocity = OtherPlayer.Mover.Velocity;
                    OtherPlayer.Mover.Velocity = Vector2.Zero;
                }

                CameraDestination = new Vector2(0, 0);

                // Figure out bounds

                float top = newScene.Top.Y;
                Transform cameraWallDown = newScene.GetComponentWithTag<Transform>("CameraWallDown");
                if (cameraWallDown?.IsAvailable == true) top = cameraWallDown.Bottom.Y;

                float bottom = newScene.Bottom.Y;
                Transform cameraWallUp = newScene.GetComponentWithTag<Transform>("CameraWallUp");
                if (cameraWallUp?.IsAvailable == true) bottom = cameraWallUp.Top.Y;

                float right = newScene.Right.X;
                Transform cameraWallLeft = newScene.GetComponentWithTag<Transform>("CameraWallLeft");
                if (cameraWallLeft?.IsAvailable == true) right = cameraWallLeft.Left.X;

                float left = newScene.Left.X;
                Transform cameraWallRight = newScene.GetComponentWithTag<Transform>("CameraWallRight");
                if (cameraWallRight?.IsAvailable == true) left = cameraWallRight.Right.X;

                Vector2 bounds = new Vector2((left - right).Abs(), (top - bottom).Abs());

                // Going Left
                if (direction.X == -1)
                {
                    CameraDestination = new Vector2(
                        right - (MyGame.VirtualWidthAccurate / 2f),
                        Transform.Position.Y
                    );

                    // Center
                    if (bounds.X - CONSTANTS.BOUNDS_MARGIN < MyGame.VirtualWidthAccurate)
                    {
                        CameraDestination.X = left + (bounds.X / 2f);
                    }

                    // Center
                    if (bounds.Y - CONSTANTS.BOUNDS_MARGIN <= MyGame.VirtualHeightAccurate)
                    {
                        CameraDestination.Y = top + (bounds.Y / 2f);
                    }
                    // Above
                    if (CameraDestination.Y - (MyGame.VirtualHeightAccurate / 2f) < top)
                    {
                        CameraDestination.Y = top + (MyGame.VirtualHeightAccurate / 2f);
                    }
                    // Below
                    if (CameraDestination.Y + (MyGame.VirtualHeightAccurate / 2f) > bottom)
                    {
                        CameraDestination.Y = bottom - (MyGame.VirtualHeightAccurate / 2f);
                    }
                }
                // Going Right
                else if (direction.X == 1)
                {
                    CameraDestination = new Vector2(
                        left + (MyGame.VirtualWidthAccurate / 2f),
                        Transform.Position.Y
                    );

                    // Center
                    if (bounds.X - CONSTANTS.BOUNDS_MARGIN < MyGame.VirtualWidthAccurate)
                    {
                        CameraDestination.X = left + (bounds.X / 2f);
                    }
                    // Center
                    if (bounds.Y - CONSTANTS.BOUNDS_MARGIN <= MyGame.VirtualHeightAccurate)
                    {
                        CameraDestination.Y = top + (bounds.Y / 2f);
                    }
                    // Above
                    if (CameraDestination.Y - (MyGame.VirtualHeightAccurate / 2f) < top)
                    {
                        CameraDestination.Y = top + (MyGame.VirtualHeightAccurate / 2f);
                    }
                    // Below
                    if (CameraDestination.Y + (MyGame.VirtualHeightAccurate / 2f) > bottom)
                    {
                        CameraDestination.Y = bottom - (MyGame.VirtualHeightAccurate / 2f);
                    }
                }
                // Going Up
                else if (direction.Y == -1)
                {
                    CameraDestination = new Vector2(
                        Transform.Position.X,
                        bottom - (MyGame.VirtualHeightAccurate / 2f)
                    );

                    // Center
                    if (bounds.Y - CONSTANTS.BOUNDS_MARGIN <= MyGame.VirtualHeightAccurate)
                    {
                        CameraDestination.Y = top + (bounds.Y / 2f);
                    }

                    // Center
                    if (bounds.X - CONSTANTS.BOUNDS_MARGIN < MyGame.VirtualWidthAccurate)
                    {
                        CameraDestination.X = left + (bounds.X / 2f);
                    }
                    // Left
                    if (CameraDestination.X - (MyGame.VirtualWidthAccurate / 2f) < left)
                    {
                        CameraDestination.X = left + (MyGame.VirtualWidthAccurate / 2f);
                    }
                    // Right
                    if (CameraDestination.X + (MyGame.VirtualWidthAccurate / 2f) > right)
                    {
                        CameraDestination.X = right - (MyGame.VirtualWidthAccurate / 2f);
                    }
                }
                // Going Down
                else if (direction.Y == 1)
                {
                    CameraDestination = new Vector2(
                        Transform.Position.X,
                        top + (MyGame.VirtualHeightAccurate / 2f)
                    );

                    // Center
                    if (bounds.Y - CONSTANTS.BOUNDS_MARGIN <= MyGame.VirtualHeightAccurate)
                    {
                        CameraDestination.Y = top + (bounds.Y / 2f);
                    }

                    // Center
                    if (bounds.X - CONSTANTS.BOUNDS_MARGIN < MyGame.VirtualWidthAccurate)
                    {
                        CameraDestination.X = left + (bounds.X / 2f);
                    }
                    // Left
                    if (CameraDestination.X - (MyGame.VirtualWidthAccurate / 2f) < left)
                    {
                        CameraDestination.X = left + (MyGame.VirtualWidthAccurate / 2f);
                    }
                    // Right
                    if (CameraDestination.X + (MyGame.VirtualWidthAccurate / 2f) > right)
                    {
                        CameraDestination.X = right - (MyGame.VirtualWidthAccurate / 2f);
                    }
                }

                // Player
                playerDirection = playerDestination - Transform.Position;
                playerReachedDestination = false;

                // Other player
                otherPlayerDirection = Vector2.Zero;
                otherPlayerReachedDestination = false;
                if (OtherPlayer?.IsAvailable == true) otherPlayerDirection = otherPlayerDestination - OtherPlayer.Transform.Position;

                // Camera
                panDirection = CameraDestination - cameraTarget.Transform.Position;

                focusRoutine++;
                focusRoutineTimer = 0.25f;

                Menu.Disable = true;

                return;
            }

            // Move camera
            if (focusRoutine == 1)
            {
                // Move Players
                if (!playerReachedDestination)
                {
                    HurtHandler.Active = false;
                    Transform.Position = Transform.Position.MoveOverTime(playerDestination, 0.001f, max: 15, min: 1);
                    Vector2 playerDistance = (Transform.Position - playerDestination).Abs();

                    if (Transform.Position == playerDestination)
                    {
                        playerReachedDestination = true;
                    }
                }
                if (OtherPlayer?.IsAvailable == true && !otherPlayerReachedDestination)
                {
                    OtherPlayer.HurtHandler.Active = false;
                    OtherPlayer.Transform.Position = OtherPlayer.Transform.Position.MoveOverTime(otherPlayerDestination, 0.001f, max: 15, min: 1);
                    Vector2 playerDistance = (OtherPlayer.Transform.Position - otherPlayerDestination).Abs();

                    if (OtherPlayer.Transform.Position == otherPlayerDestination)
                    {
                        otherPlayerReachedDestination = true;
                    }
                }

                // Move Camera
                cameraTarget.Transform.Position = cameraTarget.Transform.Position.MoveOverTime(CameraDestination, 0.001f, max: 15, min: 1);
                cameraTarget.State = "Command";
                Vector2 direction = panDirection.Sign();
                Vector2 distance = (cameraTarget.Transform.Position - CameraDestination).Abs();

                // Crossover
                List<Scene> newCrossoverScenes = MyGame.Scenes.Where(s =>
                    !crossoverScenes.Contains(s) && s != newScene && s != oldScene
                        && !s.Remove
                        && !s.IsOmnipresent
                        && s.IsRectangleInside(
                            Camera.WorldTopLeft + panDirection * panSpeed * Time.Delta,
                            Camera.WorldBottomRight + panDirection * panSpeed * Time.Delta
                        )
                ).ToList();
                if (newCrossoverScenes.Count > 0)
                {
                    newCrossoverScenes.ForEach(s =>
                    {
                        s.Visible = true;
                        s.Active = false;
                        s.LoadTilemap();
                    });
                    crossoverScenes = crossoverScenes.ConcatRange(newCrossoverScenes);
                }

                // Check reached destination
                if (cameraTarget.Transform.Position == CameraDestination)
                {
                    oldScene.Visible = false;
                    oldScene.QueueExit = true;

                    JustTransitionedScenes = true;
                    cameraTarget.State = "Player";

                    Camera.Bounds = new Bounds(
                        top: newScene.Position.Y,
                        bottom: newScene.Position.Y + newScene.Bounds.Y,
                        left: newScene.Position.X,
                        right: newScene.Position.X + newScene.Bounds.X
                    );

                    focusRoutine++;
                    focusRoutineTimer = 0.125f;
                    return;
                }

                return;
            }

            // Give player(s) back control
            if (focusRoutine == 2)
            {
                Menu.Disable = false;
                
                CheckReviveOtherPlayer();
                RefillHealth();

                UnscrambleControls();

                if (!IsGodMode)
                {
                    if (Collider.IsTouching())
                    {
                        Scooch();
                    }

                    if (OtherPlayer?.IsAvailable == true)
                    {
                        if (OtherPlayer.Collider.IsTouching())
                        {
                            OtherPlayer.Scooch();
                        }
                    }
                }

                if (newScene.GetEntityWithTag("DisableBadges") == null) Player.DisableBadges = false;
                CrushChecker.Active = true;
                Liver.Active = true;
                HurtHandler.Active = true;
                IsMovingBetweenScenes = false;
                Freeze = false;
                DisableGravity = false;
                IsInControl = true;
                breakTime = 0;
                Mover.Velocity = StoreVelocity;
                StoreVelocity = Vector2.Zero;
                HurtHandler.Active = true;
                if (Mover.Velocity.Y == 0.1f) Collider.Info.BakedDown = true;

                if (OtherPlayer?.IsAvailable == true)
                {
                    if (OtherPlayer.State == "Bubble")
                    {
                        OtherPlayer.NormalStart();
                    }
                    OtherPlayer.CrushChecker.Active = true;
                    OtherPlayer.Liver.Active = true;
                    OtherPlayer.HurtHandler.Active = true;
                    OtherPlayer.Freeze = false;
                    OtherPlayer.DisableGravity = false;
                    OtherPlayer.IsInControl = true;
                    OtherPlayer.breakTime = 0;
                    OtherPlayer.CanFocusScene = true;
                    // Revived
                    if (OtherPlayer.StoreVelocity == Vector2.Zero)
                    {
                        OtherPlayer.Mover.Velocity = Mover.Velocity;
                        OtherPlayer.didForceJump = true;
                    }
                    // Normal
                    else
                    {
                        OtherPlayer.Mover.Velocity = OtherPlayer.StoreVelocity;
                    }
                    OtherPlayer.StoreVelocity = Vector2.Zero;
                    OtherPlayer.HurtHandler.Active = true;
                    if (OtherPlayer.Mover.Velocity.Y == 0.1f) OtherPlayer.Collider.Info.BakedDown = true;

                    if (!IsTouchingWater)
                    {
                        if (OtherPlayer.State == "Swim") OtherPlayer.NormalStart();
                    }
                }

                crossoverScenes.ForEach(s =>
                {
                    s.Visible = false;
                    s.QueueExit = true;
                });

                crossoverScenes.Clear();

                // I don't give a shit :^)
                newScene = null;
                focusRoutine = null;
                CanFocusScene = true;
                focusRoutine = null;
                focusRoutineTimer = 0;
                oldScene = null;
                playerDestination = new Vector2(0, 0);
                playerDirection = new Vector2(0, 0);
                playerReachedDestination = false;
                otherPlayerDestination = new Vector2(0, 0);
                otherPlayerDirection = Vector2.Zero;
                otherPlayerReachedDestination = false;
                CameraDestination = new Vector2(0, 0);
                panDirection = new Vector2(0, 0);

                focusRoutine++;
                return;
            }
        }

        public void Teleport(Vector2 position)
        {
            Factory.ParticleEffect(
                scene: Entity.Scene,
                position: Transform.Position,
                amount: 5,
                speed: new Range(4f, 6f),
                duration: 0.125f,
                spriteTemplate: new Sprite("MiniStar", 8, 1, Color.White),
                directionType: ParticleDirection.Out
            );

            Shatterer.Shatter();

            Transform.Position = position;
            Collider.Info.Clear();
            Mover.Velocity = new Vector2(0, 0);

            Factory.SfxPlayer(name: "Teleport", pitch: 1f);

            Factory.ParticleEffect(
                scene: Entity.Scene,
                position: position,
                amount: 5,
                speed: new Range(4f, 6f),
                duration: 0.125f,
                spriteTemplate: new Sprite("MiniStar", 8, 1, Color.White),
                directionType: ParticleDirection.Out
            );
        }

        void CheckFallableTiles()
        {
            Transform fallingTile = Collider.Info.ActorCollisions.Find(c => c.Entity.Tags.Contains("Fallable"));
            if (fallingTile != null)
            {
                fallingTile.Entity.GetComponent<FallingTile>().Fall();
            }
        }

        void CheckBananas()
        {
            Transform banana = Collider.GetCollision("Banana");

            if (banana != null)
            {
                banana.Entity.GetComponent<Shatterer>().Shatter();
                LeapStart(false, banana.Entity.GetComponent<Sprite>().FlipHorizontally ? -1 : 1);
                banana.Entity.Destroy();
            }
        }

        void CheckTreadmills()
        {
            List<Transform> list = GravityChanger.Direction.Y == 1
                ? Collider.Info.ActorCollisionDown
                : GravityChanger.Direction.Y == -1
                    ? Collider.Info.ActorCollisionUp
                    : GravityChanger.Direction.X == 1
                        ? Collider.Info.ActorCollisionRight
                        : GravityChanger.Direction.X == -1
                            ? Collider.Info.ActorCollisionLeft
                            : null;

            if (list == null) return;

            Transform treadmill = list.Find(c =>
                c.Entity.Tags.Contains("Treadmill") && c.Entity.GetComponent<Treadmill>()?.Enabled == true
            );

            if (treadmill == null) return;

            float withSpeed = 115f;
            float normalSpeed = 65f;
            float againstSpeed = 0;
            bool left = treadmill.Entity.Tags.Contains("TreadmillLeft");

            // Can be affected by treadmill
            if ((State == "Normal" || State == "Bounce" || (State == "Leap" && canExitLeap)))
            {
                // Treadmill Left
                if (left)
                {
                    if (IsRunning && moveDir < 0) Mover.Velocity.X = -withSpeed - (runSpeed - moveSpeed);
                    else if (IsRunning && moveDir > 0) Mover.Velocity.X = -againstSpeed + (runSpeed - moveSpeed);
                    else if (State == "Normal" && input.X < 0) Mover.Velocity.X = -withSpeed;
                    else if (State == "Normal" && input.X > 0) Mover.Velocity.X = -againstSpeed;
                    else Mover.Velocity.X = -normalSpeed;
                }
                // Treadmill Right
                else
                {
                    if (IsRunning && moveDir > 0) Mover.Velocity.X = withSpeed + (runSpeed - moveSpeed);
                    else if (IsRunning && moveDir < 0) Mover.Velocity.X = againstSpeed - (runSpeed - moveSpeed);
                    else if (State == "Normal" && input.X > 0) Mover.Velocity.X = withSpeed;
                    else if (State == "Normal" && input.X < 0) Mover.Velocity.X = againstSpeed;
                    else Mover.Velocity.X = normalSpeed;
                }
            }
            
        }

        void CheckOtherPlayer()
        {
            if (MyGame.PlayerCount == 1 || OtherPlayer?.IsAvailable == true) return;

            OtherPlayer = Entity.Tags.Contains("PlayerOne")
                ? MyGame.GetComponentWithTag<Player>("PlayerTwo")
                : MyGame.GetComponentWithTag<Player>("PlayerOne");
        }

        public bool IsInsideCameraBounds()
        {
            if (Transform == null) return false;

            if (GravityChanger.Name != "Up")
            {
                if (Transform.Top.Y > MyGame.CurrentScene.CameraBottom.Y) return false;
            }

            if (GravityChanger.Name != "Down")
            {
                if (Transform.Bottom.Y < MyGame.CurrentScene.CameraTop.Y) return false;
            }

            if (GravityChanger.Name != "Left")
            {
                if (Transform.Left.X > MyGame.CurrentScene.CameraRight.X) return false;
            }

            if (GravityChanger.Name != "Right")
            {
                if (Transform.Right.X < MyGame.CurrentScene.CameraLeft.X) return false;
            }

            return true;
        }

        public bool IsSafe()
        {
            var collision = Collider.GetHitInDirection(
                standardBottom,
                GravityChanger.Direction,
                4,
                new List<string>() { "Collision" },
                new List<string>() { "No" },
                2
            );

            // No ground, check for water?
            if (collision == null)
            {
                collision = Collider.GetHitInDirection(
                    standardBottom,
                    GravityChanger.Direction,
                    4,
                    new List<string>() { "Water" },
                    new List<string>() { "No" },
                    2
                );
            }

            return collision != null;
        }

        void CheckDistanceToOtherPlayer()
        {
            if (MyGame.PlayerCount == 1 || OtherPlayer?.IsAvailable != true || State == "Transport" || OtherPlayer.State == "Transport" || State == "Bubble" || OtherPlayer.State == "Bubble" || cameraTarget == null
                || cameraTarget.State == "Command" || cameraTarget.State == "Pan" || cameraTarget.State == "ReturnToPlayer" || Liver.Health <= 0 || MyGame.CurrentScene.NoTether) return;

            Vector2 distance = (Transform.Position - OtherPlayer.Transform.Position).Abs();

            bool tooFar = false;

            if (MyGame.CurrentScene.IsTransformInside(Transform) && MyGame.CurrentScene.IsTransformInside(OtherPlayer.Transform) && IsInsideCameraBounds() && OtherPlayer.IsInsideCameraBounds())
            {
                // X
                if (distance.X >= 64)
                {
                    float sceneLeft = MyGame.CurrentScene.GetComponentWithTag<Transform>("CameraWallRight")?.Right.X ?? MyGame.CurrentScene.Left.X;
                    float sceneRight = MyGame.CurrentScene.GetComponentWithTag<Transform>("CameraWallLeft")?.Left.X ?? MyGame.CurrentScene.Right.X;

                    if (
                        (
                            Transform.Position.X <= Camera.WorldLeft.X - 18
                                && Camera.WorldLeft.X >= sceneLeft + 16
                        )
                            || (
                                Transform.Position.X >= Camera.WorldRight.X + 18
                                    && Camera.WorldRight.X <= sceneRight - 16
                                )
                    )
                    {
                        tooFar = true;
                    }
                }

                // Y
                if (distance.Y >= 64)
                {
                    float sceneTop = MyGame.CurrentScene.GetComponentWithTag<Transform>("CameraWallDown")?.Bottom.Y ?? MyGame.CurrentScene.Top.Y;
                    float sceneBottom = MyGame.CurrentScene.GetComponentWithTag<Transform>("CameraWallUp")?.Top.Y ?? MyGame.CurrentScene.Bottom.Y;

                    if (
                        (
                            Transform.Position.Y <= Camera.WorldTop.Y - 32
                                && Camera.WorldTop.Y >= sceneTop + 16
                        )
                        || (
                            Transform.Position.Y >= Camera.WorldBottom.Y + 32
                                && Camera.WorldBottom.Y <= sceneBottom - 16
                            )
                    )
                    {
                        tooFar = true;
                    }
                }
            }

            if (tooFar)
            {
                List<Player> orderedPlayers = new List<Player>() { this, OtherPlayer }
                    .OrderByDescending(p => p.Transform.Position.Distance(SaveData.Current.Checkpoint.Position))
                    .ToList();

                Player progressingPlayer = orderedPlayers[0];
                Player nonProgressingPlayer = orderedPlayers[1];

                // Check if progressing player is over something safe and switch if nonProgressingPlayer is safe.
                if (!progressingPlayer.IsSafe() && nonProgressingPlayer.IsSafe())
                {
                    progressingPlayer = orderedPlayers[1];
                    nonProgressingPlayer = orderedPlayers[0];
                }

                nonProgressingPlayer.TransportStart();
            }
        }

        public void RefillHealth()
        {
            if (breakBehavior?.IsAvailable == true)
            {
                if (breakBehavior.Charge != breakBehavior.ChargeMax) breakBehavior.Reset();
                breakBehavior.Charge = breakBehavior.ChargeMax;
            }
            if (SaveData.Current?.Recover != "Room") return;

            bool recovered = false;
            if (Liver.Health != Liver.MaxHealth) recovered = true;
            Liver.Health = Liver.MaxHealth;
            if (recovered)
            {
                HealTime = 1.5f;
                HurtTime = 0;
                Factory.SfxPlayer(name: "HeartDrop", pitch: -0.125f);
            }
            if (recovered) UiHealthBar.Popup(recover: true);

            if (OtherPlayer?.IsAvailable == true && OtherPlayer?.Liver.Health > 0)
            {
                if (OtherPlayer.breakBehavior?.IsAvailable == true)
                {
                    if (OtherPlayer.breakBehavior.Charge != OtherPlayer.breakBehavior.ChargeMax) OtherPlayer.breakBehavior.Reset();
                    OtherPlayer.breakBehavior.Charge = OtherPlayer.breakBehavior.ChargeMax;
                }

                bool recoveredTwo = false;
                if (OtherPlayer.Liver.Health != OtherPlayer.Liver.MaxHealth) recoveredTwo = true;
                OtherPlayer.Liver.Health = OtherPlayer.Liver.MaxHealth;
                if (recoveredTwo)
                {
                    OtherPlayer.HealTime = 1.5f;
                    OtherPlayer.HurtTime = 0;
                    if (!recovered) Factory.SfxPlayer(name: "HeartDrop", pitch: -0.125f);
                }
                if (recoveredTwo) OtherPlayer.UiHealthBar.Popup(recover: true);
            }
        }

        Color angryPulseColor = Paint.White;
        float angryPulseTime = 0f;
        int angryShake = 0;
        Color angryColor = Paint.White;

        void CheckHealthShake()
        {
            if (Liver.Health > 1 || Liver.MaxHealth == 1 || HurtHandler.IsInvincible || Liver.Health <= 0 || Flicker || State == "Bounce")
            {
                angryColor = Paint.White;
                return;
            }

            angryPulseTime -= Time.Delta;
            if (angryPulseTime <= 0)
            {
                angryPulseTime = 1f;
                angryShake--;

                if (angryShake <= 0)
                {
                    Factory.SfxPlayer(name: "MechWiggle", volume: 0.125f, pitch: 1f);
                    Controller.Rumble(0.1f, 0.1f);
                    Shaker.ShakeX(1);
                    Factory.ParticleEffect(
                        position: Transform.Position,
                        amount: Chance.Range(2, 4),
                        speed: new Range(4f, 6f),
                        duration: 0.125f,
                        spriteTemplate: new Sprite("MiniStar", 8, 1, Paint.Pink),
                        directionType: ParticleDirection.Out
                    );

                    angryShake = 6;
                }

                if (angryPulseColor == Paint.White) angryPulseColor = Paint.HexToColor("#ffbaea");
                else angryPulseColor = Paint.White;
            }
            if (!HurtHandler.IsBlinking)
            {
                angryColor = Sprite.Color.MoveOverTime(angryPulseColor, 0.1f, min: 0);
            } 

            // if (lowHealthTimer?.IsAvailable != true)
            // {
            //     if (!MyGame.IsTalking)
            //     {
            //         Controller.Rumble(0.1f, 0.1f);
            //         Factory.SfxPlayer(name: "MechWiggle", volume: 0.125f, pitch: 1f);
            //     }

            //     TintOverride = Paint.Pink;
            //     Factory.OneShotTimer(end: _ =>
            //     {
            //         TintOverride = null;
            //     }, duration: 0.1f);

            //     Factory.ParticleEffect(
            //         position: Transform.Position,
            //         amount: Chance.Range(2, 4),
            //         speed: new Range(4f, 6f),
            //         duration: 0.125f,
            //         spriteTemplate: new Sprite("MiniStar", 8, 1, Paint.Pink),
            //         directionType: ParticleDirection.Out
            //     );

            //     lowHealthTimer = Factory.Timer(
            //         scene: MyGame.PlayerContainer,
            //         duration: 1.5f,
            //         end: (Timer t) =>
            //         {
            //             Shaker.ShakeX(1);
            //             t.Entity.Destroy();
            //         }
            //     );
            // }
        }

        // ---

        void CheckCostume()
        {
            if (PlayerProfile.Changed)
            {
                Sprite.Texture = PlayerProfile.PlayerTexture;
                HurtHandler.HitSound = $"{PlayerProfile.Costume}Hit";
            }
        }

        void ExitFallOffScreen()
        {
            fellOffScreen = false;
            fellOffScreenCanRecover = true;
            Sprite.Rotation = GravityChanger.Rotation;
        }

        void HandleSpit()
        {
            // if (isStupid) return;

            bool spit = Controller.Spit.IsHeld && canSpit;
            bool hairball = BadgeIsHeld(Badge.Hairball) && canHairball;
            if (spit || hairball)
            {
                if (fellOffScreen && fellOffScreenCanRecover) ExitFallOffScreen();
                // Stamina
                // stamina -= 1f;
                // staminaDisplayTime = 0;
                // if (stamina <= 0) 
                // {
                //     waitForStamina = true;
                //     stamina = -1f;
                //     StaminaBar.Color = Paint.HexToColor("#b25266");
                //     StaminaBar.BackgroundColor = Paint.HexToColor("#f6a2a8");
                // }

                Vector2 spitPosition = Vector2.Zero;
                Vector2 spitDirection = new Vector2(0, 0);
                Vector2 hairballSpeed = new Vector2(0, 0);

                if (Animator.State.Name.Contains("Swim"))
                {
                    spitDirection = new Vector2(MathF.Cos(Sprite.Rotation), MathF.Sin(Sprite.Rotation));
                    if (spitDirection.X.Abs() < 0.1f) spitDirection.X = 0;
                    if (spitDirection.Y.Abs() < 0.1f) spitDirection.Y = 0;
                    spitPosition = Transform.Position + (spitDirection.Sign() * 12);

                    if (hairball)
                    {
                        hairballSpeed = Mover.Velocity;
                        hairballSpeed += spitDirection * 50f;
                    }
                }
                else if (Animator.State.Name.Contains("Leap"))
                {
                    if (GravityChanger.IsVertical)
                    {
                        // Up
                        if (input.Y < 0)
                        {
                            spitPosition = Transform.Position + new Vector2(
                                facingDirection * 6, 
                                GravityChanger.Direction.Y == 1 ? -8 : -8
                            );
                            spitDirection = new Vector2(0, -1);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.Y += -200;
                            }
                        }
                        // Down
                        else if (input.Y > 0)
                        {
                            spitPosition = Transform.Position + new Vector2(
                                facingDirection * 6, 
                                GravityChanger.Direction.Y == 1 ? 8 : 8
                            );
                            spitDirection = new Vector2(0, 1);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.Y += 200;
                            }
                        }
                        // Left/Right
                        else
                        {
                            spitPosition = Transform.Position + new Vector2(
                                facingDirection == -1 ? -12 : 12, 
                                GravityChanger.Direction.Y == 1 ? 2 : -2
                            );
                            spitDirection = new Vector2(facingDirection == -1 ? -1 : 1, 0);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += 90f * spitDirection.X;
                                hairballSpeed.Y += -80f * GravityChanger.Direction.Y;
                            }
                        }
                    }
                    else
                    {
                        // Left
                        if (Controller.Left.IsHeld)
                        {
                            spitPosition = Transform.Position + new Vector2(
                                GravityChanger.Direction.X == 1 ? -8 : -8, 
                                facingDirection * 6
                            );
                            spitDirection = new Vector2(-1, 0);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += -200;
                            }
                        }
                        // Right
                        else if (Controller.Right.IsHeld)
                        {
                            spitPosition = Transform.Position + new Vector2(
                                GravityChanger.Direction.X == 1 ? 8 : 8, 
                                facingDirection * 6
                            );
                            spitDirection = new Vector2(1, 0);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += 200;
                            }
                        }
                        // Up/Down
                        else
                        {
                            spitPosition = Transform.Position + new Vector2(
                                GravityChanger.Direction.X == 1 ? 2 : -2, 
                                facingDirection == -1 ? -12 : 12
                            );
                            spitDirection = new Vector2(0, facingDirection == -1 ? -1 : 1);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += -80f *  GravityChanger.Direction.X;
                                hairballSpeed.Y += 90f * spitDirection.Y;
                            }
                        }
                    }
                }
                else
                {
                    if (GravityChanger.IsVertical)
                    {
                        // Up
                        if (input.Y < 0)
                        {
                            spitPosition = Transform.Position + new Vector2(0, GravityChanger.Direction.Y == 1 ? -12 : -6);
                            spitDirection = new Vector2(0, -1);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.Y += -200;
                            }
                        }
                        // Down
                        else if (input.Y > 0)
                        {
                            spitPosition = Transform.Position + new Vector2(0, GravityChanger.Direction.Y == 1 ? 6 : 12);
                            spitDirection = new Vector2(0, 1);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.Y += 200;
                            }
                        }
                        // Left/Right
                        else
                        {
                            spitPosition = Transform.Position + new Vector2(facingDirection == -1 ? -8 : 8, GravityChanger.Direction.Y == 1 ? -4 : 4);
                            spitDirection = new Vector2(facingDirection == -1 ? -1 : 1, 0);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += 90f * spitDirection.X;
                                hairballSpeed.Y += -80f * GravityChanger.Direction.Y;
                            }
                        }
                    }
                    else
                    {
                        // Left
                        if (Controller.Left.IsHeld)
                        {
                            spitPosition = Transform.Position + new Vector2(GravityChanger.Direction.X == 1 ? -12 : -6, 0);
                            spitDirection = new Vector2(-1, 0);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += -200;
                            }
                        }
                        // Right
                        else if (Controller.Right.IsHeld)
                        {
                            spitPosition = Transform.Position + new Vector2(GravityChanger.Direction.X == 1 ? 6 : 12, 0);
                            spitDirection = new Vector2(1, 0);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += 200;
                            }
                        }
                        // Up/Down
                        else
                        {
                            spitPosition = Transform.Position + new Vector2(GravityChanger.Direction.X == 1 ? -4 : 4, facingDirection == -1 ? -8 : 8);
                            spitDirection = new Vector2(0, facingDirection == -1 ? -1 : 1);

                            if (hairball)
                            {
                                hairballSpeed = Mover.Velocity;
                                hairballSpeed.X += -80f *  GravityChanger.Direction.X;
                                hairballSpeed.Y += 90f * spitDirection.Y;
                            }
                        }
                    }
                }


                // Spit Kickback
                // if (spitDirection.X != 0)
                // {
                //     Mover.Velocity.X = 0;
                //     Mover.Velocity.X -= spitDirection.X * spitKickback.X;
                // }
                // if (spitDirection.Y == -1 || (spitDirection.Y == 1 && Mover.Velocity.Y.Sign() != -1))
                // {
                //     Mover.Velocity.Y = 0;
                //     Mover.Velocity.Y -= spitDirection.Y * spitKickback.Y;
                // }

                if (hairball)
                {
                    Factory.SfxPlayer(name: $"{PlayerProfile.Costume}Spit", volume: 0.5f, parent: Entity, pitch: Chance.Range(-0.8f, -0.6f), onlyOne: false);
                    hairballTime = hairballDuration;
                    spitTime = spitDuration;

                    if (hairballInstance?.IsAvailable == true) hairballInstance.Entity.GetComponent<Liver>().LoseHealth(99999);

                    hairballInstance = Factory.Hairball(
                        Entity.Scene,
                        Transform.Position,
                        hairballSpeed,
                        identity == "PlayerOne",
                        1,
                        this
                    ).GetComponent<Collider>();
                }
                else if (spit)
                {
                    Factory.SfxPlayer(name: spitSfx.Random(), volume: 0.35f, parent: Entity, pitch: Chance.Range(-0.5f, 0.25f), onlyOne: false);
                    spitTime = spitDuration;

                    Controller.Rumble(0.1f, 0.05f);

                    spitInstance = Factory.Spit(
                        Entity.Scene,
                        spitPosition,
                        spitDirection,
                        Entity.Tags.Contains("PlayerOne") ? "OwnerOne" : "OwnerTwo",
                        PlayerProfile.Costume,
                        Paint.White,
                        this
                    ).GetComponent<Collider>();
                }
            }
        }

        // void HandleHomingWad()
        // {
        //     if (!BadgeIsEquipped(Badge.HomingWad))
        //     {
        //         homingWadTime = 0;
        //         foreach (Entity wad in homingWads.Where(w => w?.IsAvailable == true).ToList()) wad.Destroy();
        //         homingWads = Enumerable.Repeat((Entity)null, HomingWadMax).ToList();
        //         homingWadInterval = 1;
        //         return;
        //     }

        //     int i = 0;
        //     foreach (Entity wad in homingWads.ToList())
        //     {
        //         if (wad != null && wad.Remove) homingWads[i] = null;
        //         i++;
        //     }

        //     if (BadgeIsHeld(Badge.HomingWad) && canHomingWad)
        //     {
        //         Factory.SfxPlayer(Entity.Scene, "Shoot", Transform.Position, volume: 0.25f);

        //         int freeIndex = homingWads.IndexOf(null);
        //         Entity homingWad = Factory.HomingWad(Entity.Scene, Transform.Position + new Vector2(0, 4), Transform, freeIndex);
        //         homingWads[freeIndex] = homingWad;
        //         homingWadTime = homingWadDuration;
        //         homingWadInterval++;
        //         if (homingWadInterval > HomingWadMax)
        //         {
        //             homingWadInterval = 1;
        //         }
        //     }
        // }

        void HandleBark()
        {
            if ((BadgeIsHeld(Badge.Bark) && canBark) || isBarking && barkIntervalTime <= 0)
            {
                if (!isBarking)
                {
                    Factory.SfxPlayer(Entity.Scene, $"{PlayerProfile.Costume}Bark", Transform.Position, 0.5f, pitch: Chance.Range(-0.25f, 0.25f));
                }

                if (fellOffScreen && fellOffScreenCanRecover) ExitFallOffScreen();

                Factory.Bark(
                    Entity.Scene,
                    Transform.Position,
                    Entity.Tags.Contains("PlayerOne") ? "PlayerOne" : "PlayerTwo",
                    Transform,
                    PlayerProfile.Costume,
                    this
                );

                barkCount++;

                if (barkCount >= barkMax)
                {
                    isBarking = false;
                    barkCount = 0;
                    barkTime = barkDuration;
                    spitTime = spitDuration;
                }
                else
                {
                    isBarking = true;
                    barkIntervalTime = BarkIntervalDuration;
                }
            }
        }

        #region States

        public void ChangeState(string state)
        {
            // Exit previous state
            switch (State)
            {
                case "Normal":
                    NormalExit(state);
                    break;
                case "Swim":
                    SwimExit(state);
                    break;
                case "Recoil":
                    RecoilExit(state);
                    break;
                case "Leap":
                    LeapExit(state);
                    break;
                case "Dash":
                    DashExit(state);
                    break;
                case "Talk":
                    TalkExit(state);
                    break;
                case "Target":
                    TargetExit(state);
                    break;
                case "Stand":
                    StandExit(state);
                    break;
                case "Launch":
                    LaunchExit(state);
                    break;
                case "Win":
                    WinExit(state);
                    break;
                case "Ball":
                    BallExit(state);
                    break;
                case "Lerp":
                    LerpExit(state);
                    break;
                case "Stop":
                    StopExit(state);
                    break;
                case "Bounce":
                    BounceExit(state);
                    break;
                case "PingPong":
                    PingPongExit(state);
                    break;
                case "Bubble":
                    BubbleExit(state);
                    break;
                case "Parry":
                    ParryExit(state);
                    break;
                case "Transport":
                    TransportExit(state);
                    break;
                case "Command":
                    CommandExit(state);
                    break;
                default:
                    NormalExit(state);
                    break;
            }

            State = state;
        }

        // Stop

        public void StopStart()
        {
            Mover.Velocity = Vector2.Zero;
            HurtHandler.Active = false;
            ChangeState("Stop");
        }

        public void Stop()
        {
            Mover.Velocity = Vector2.Zero;
        }

        public void StopExit(string state)
        {
            HurtHandler.Active = true;
        }

        // Parry

        public void ParryStart()
        {
            IsRunning = false;
            IsFluttering = false;
            lockRunEffects = true;
            HurtHandler.Active = false;
            parryTime = parryDuration;
            ChangeState("Parry");
            Factory.SfxPlayer(name: "Tink", pitch: 1f);
            Factory.TinkParticleEffect(position: Transform.Position, color: Paint.White);
            didParry = false;
            // TintOverride = Paint.Black;
        }

        public void Parry()
        {
            Animator.Offset = 0;
            animationState = new AnimationState(name: "Parry", 46);
            parryTime -= Time.ModifiedDelta;

            if (parryTime <= parryDuration - parryActiveDuration)
            {
                if (!IsTouchingWater) Mover.Velocity.V_Add(CONSTANTS.GRAVITY * 0.85f * Time.ModifiedDelta);
                Sprite.Rotation += 2 * facingDirection * Time.ModifiedDelta;
                Sprite.Position = Utility.Shake(2);
                HurtHandler.Active = true;
                TintOverride = null;
                lockRunEffects = false;
                animationState = new AnimationState(name: "ParryRecover", 1);
            }
            else
            {
                TintOverride = Paint.Rainbow.Random();
                Mover.Velocity = Mover.Velocity.MoveOverTime(Vector2.Zero, 0.005f);
                Sprite.Rotation += (40 + Chance.Range(-20, 20)) * facingDirection * Time.ModifiedDelta;
                Transform hit = Collider.GetCollision(HurtHandler.Weaknesses);
                if (hit?.IsAvailable == true && !didParry)
                {
                    HurtHandler.TriggerInvincibility(1f);
                    didParry = true;
                    Factory.SfxPlayer(name: "HeartDrop", pitch: 0.25f);
                    if (hit.Entity.Name == "Shockwave")
                    {
                        hit.Entity.GetComponent<Bumper>()?.Stun();
                    }

                    Shaker hitShaker = hit.Entity.GetComponent<Shaker>();
                    if (hitShaker?.IsAvailable == true) if (Chance.CoinFlip()) hitShaker.ShakeX(); else hitShaker.ShakeY();
                    // Hurter hitHurter = hit.Entity.GetComponent<Hurter>();
                    // if (hitHurtHandler?.IsAvailable == true && hitHurtHandler.Indestructible) hitHurtHandler.Deflect(Mover);
                    // else hitLiver.LoseHealth(Hurter);
                    // if (hitHurter?.IsAvailable == true && hitHurter.LandedHit != null) hitHurter.LandedHit(hitHurter, Entity);

                    RecoverHealth();
                    if (IsTouchingWater) SwimStart();
                    else NormalStart();
                }
            }
            if (parryTime <= 0)
            {
                if (IsTouchingWater) SwimStart();
                else NormalStart();
            }
        }

        public void ParryExit(string state)
        {
            Sprite.Rotation = 0;
            HurtHandler.Active = true;
            TintOverride = null;
            lockRunEffects = false;
            IsFluttering = false;
        }

        // Transport

        public void NotNow()
        {
            Factory.SfxPlayer(name: "NuUh", pitch: 0.5f);
            Factory.FadingText(position: Transform.Top + new Vector2(0, -8) + Utility.Shake(8), text: Dial.Key("NOT_NOW"), color: Paint.Red, outlineColor: Paint.Black, duration: 0.5f);
            Shaker.ShakeX();
        }

        public void Nope()
        {
            Factory.SfxPlayer(name: "NuUh", pitch: 0.5f);
            Factory.FadingText(position: Transform.Top + new Vector2(0, -8) + Utility.Shake(8), text: Dial.Key("NOPE"), color: Paint.Red, outlineColor: Paint.Black, duration: 0.5f);
            Shaker.ShakeX();
        }

        public void TransportStart(bool manual = false, Transform target = null)
        {
            if (target == null)
            {
                if (!MyGame.IsTwoPlayer || OtherPlayer?.IsAvailable != true || OtherPlayer.State == "Transport" || (manual && transportManualTime > 0))
                {
                    Nope();
                    return;
                }
                transportTarget = OtherPlayer?.Transform;
            }
            else
            {
                transportTarget = target;
            }

            Collider.DoesCollideWithGround = false;
            transportManual = manual;
            if (manual) transportManualTime = 1.5f;
            ChangeState("Transport");
            HurtHandler.TriggerInvincibility(10f);
            Factory.SfxPlayer(name: "Teleport", pitch: -0.5f);
            IsRunning = false;
            IsFluttering = false;
        }

        public void Transport()
        {
            if (transportTarget?.IsAvailable != true)
            {
                Entity.Destroy();
                return;
            }
            Mover.Velocity = Vector2.Zero;
            Transform.Position = Transform.Position.MoveOverTime(transportTarget.Position, 0.01f, min: 4f);
            Sprite.Rotation += ((transportTarget.Position.X - Transform.Position.X) + (transportTarget.Position.Y - Transform.Position.Y)) * 0.5f * Time.Delta;

            if (MyGame.Cycle % 3 == 0)
            {
                Factory.DustParticleEffect(position: Transform.Position, color: Paint.White, fadeOverTime: true, size: new Vector2(8, 8));
            }

            if (!Player.IsMovingBetweenScenes)
            {
                if (Transform.Position.Distance(transportTarget.Position) <= 8)
                {
                    NormalStart();
                    if (!IsTouchingWater)
                    {
                        Mover.Velocity.V(minJumpSpeed * 0.75f);
                        Mover.Velocity.H(0);
                        Collider.Info.Clear();
                        didForceJump = true;
                        dampenGravity = true;
                        Sprite.Rotation = GravityChanger.Rotation;
                        Factory.SfxPlayer(name: "Jump", pitch: 0.5f);
                        Factory.StarParticleEffect(position: Transform.Position);
                    }
                    if (transportManual) HurtHandler.TriggerInvincibility(0.25f);
                    else HurtHandler.TriggerInvincibility(1.5f);
                }
            }
        }

        public void TransportExit(string state)
        {
            Collider.DoesCollideWithGround = true;
        }

        // Bubble

        public void BubbleStart()
        {
            ChangeState("Bubble");
            cameraTarget.State = "ReturnToPlayer";
            Mover.Active = false;
            animationState = new AnimationState("ContactHurt", 10);
            Animator.Offset = 0;
            Controller.Rumble(0.25f, 0.25f);

            Mover.Velocity = Vector2.Zero;
            // Teleport(progressingPlayer.Transform.Position);
            HurtHandler.TriggerInvincibility(1000f);
            BubbleSafe = false;
            BubbleEscape = false;
            Factory.SfxPlayer(name: "Teleport", pitch: 0.25f);

            float elapsed = 0;
            Entity holdup = new Entity("Holdup", MyGame.PlayerContainer);
            holdup.AddComponent(new Activity(_ =>
            {
                elapsed += Time.Delta;

                if (OtherPlayer?.Entity?.IsAvailable != true)
                {
                    Liver.LoseHealth(999999);
                    Freeze = false;

                    holdup.Destroy();
                }

                Vector2 destination = OtherPlayer.Transform.Top
                    + new Vector2(0, -8)
                    + new Vector2(
                        MathF.Sin(elapsed * 2f) * 16,
                        MathF.Cos(elapsed * 2f) * 8
                    );
                Transform.Position = Transform.Position.MoveOverTime(destination, 0.01f);

                Sprite.Rotation += Time.Delta * 2f;

                if (BubbleEscape)
                {
                    Factory.SfxPlayer(name: "Jump", pitch: 0.5f);
                    TintOverride = null;

                    BubbleSafe = false;
                    BubbleEscape = false;
                    Sprite.Position = Vector2.Zero;
                    Sprite.Rotation = GravityChanger.Rotation;
                    Freeze = false;
                    Shaker.ShakeX();
                    HurtHandler.TriggerInvincibility(1f);
                    if (Collider.IsTouching(
                        new List<Vector2>() { Transform.TopLeft, Transform.TopRight, Transform.BottomLeft, Transform.BottomRight },
                        new List<string>() { "Collision", "No" })
                    )
                    {
                        Transform.Position = OtherPlayer.Transform.Position;
                    }


                    holdup.Destroy();
                }
                else
                {
                    if (elapsed >= 1f && cameraTarget.State != "ReturnToPlayer")
                    {
                        if (!BubbleSafe)
                        {
                            Shaker.ShakeY();
                            HurtHandler.ResetInvincibility();
                        }
                        else
                        {
                            HurtHandler.TriggerInvincibility(1000f);
                        }
                        BubbleSafe = true;
                    }
                }
            }));
        }

        void Bubble()
        {
            if (BubbleSafe)
            {
                animationState = new AnimationState("Safe", 1);
                TintOverride = null;
                if (Controller.Jump.IsPressed)
                {
                    NormalStart();
                }
            }
            else
            {
                animationState = new AnimationState("ContactHurt", 10);
                TintOverride = Paint.Pink;
                if (Controller.Jump.IsPressed)
                {
                    Factory.SfxPlayer(name: "NuUh", pitch: 0.5f);
                    Factory.FadingText(position: Transform.Top + new Vector2(0, -8) + Utility.Shake(8), text: Dial.Key("NOPE"), color: Paint.Red, outlineColor: Paint.Black, duration: 0.5f);
                    Shaker.ShakeX();
                }
            }
        }

        void BubbleExit(string state)
        {
            TintOverride = null;
            Mover.Active = true;
            Controller.Rumble(0.125f, 0.125f);
            BubbleEscape = true;
        }

        // Normal

        void Flutter()
        {
            if (GravityChanger.Direction.Y == 1) Mover.Velocity.Y = flutterSpeed;
            if (GravityChanger.Direction.Y == -1) Mover.Velocity.Y = -flutterSpeed;
            if (GravityChanger.Direction.X == 1) Mover.Velocity.X = flutterSpeed;
            if (GravityChanger.Direction.X == -1) Mover.Velocity.X = -flutterSpeed;
            IsFluttering = true;
            DidJumpFromSpring = false;
            didForceJump = false;
            dampenGravity = false;
            didJump = true;
            flutterBuffer = 0;
            if (fellOffScreen && fellOffScreenCanRecover)
            {
                ExitFallOffScreen();
            }
            if (State != "Normal") NormalStart();
        }

        public void CommandStart()
        {
            ChangeState("Command");
            Mover.Velocity = Vector2.Zero;
            HurtHandler.Active = false;
            animationState = null;
        }

        public void CommandExit(string state)
        {
            HurtHandler.Active = true;
        }

        public void NormalStart()
        {
            ChangeState("Normal");
        }

        void Normal()
        {
            if (IsTouchingWater)
            {
                SwimStart();
                return;
            }

            if (!Controller.Up.IsHeld) didReleaseInputSinceDoor = true;
            if (Pusher?.IsAvailable == true) Pusher.Active = true;

            // Update timers
            gravityGraceTime -= Time.ModifiedDelta;

            if (input.X == 0 && !IsRunning) gravityGraceTime = 0;

            if (BadgeIsPressed(Badge.Parry))
            {
                if (HurtHandler.IsInvincible)
                {
                    Factory.SfxPlayer(name: "NuUh");
                    Shaker.ShakeY();
                    Factory.FadingText(position: Transform.Top + new Vector2(0, -8) + Utility.Shake(8), text: Dial.Key("NOPE"), color: Paint.Red, outlineColor: Paint.Black, duration: 0.5f);
                }
                else
                {
                    ParryStart();
                    return;
                }
            }

            // Set Gravity

            if (IsTouchingAntiGravity)
            {
                if (Controller.Jump.IsHeld || IsFluttering)
                {
                    if ((
                        (GravityChanger.Direction.Y == 1 && Mover.Velocity.Y < -maxFallSpeed) 
                            || (GravityChanger.Direction.Y == -1 && Mover.Velocity.Y > maxFallSpeed)
                            || (GravityChanger.Direction.X == 1 && Mover.Velocity.X < -maxFallSpeed) 
                            || (GravityChanger.Direction.X == -1 && Mover.Velocity.X > maxFallSpeed)

                        ) && !IsFluttering) 
                        gravity = 0;
                    else if (IsFluttering)  gravity = CONSTANTS.GRAVITY / 4f;
                    else gravity = CONSTANTS.GRAVITY / 2f;
                } 
                else gravity = CONSTANTS.GRAVITY / 2f;
            }
            else
            {
                if (Controller.Jump.IsHeld || didForceJump || DidJumpFromSpring || didJumpFromWater || fellOffScreenAndInAir) gravity = CONSTANTS.GRAVITY;
                else gravity = CONSTANTS.GRAVITY * 2f;

                if (
                    IsFluttering && !(didForceJump || DidJumpFromSpring) 
                        && (
                            (GravityChanger.Direction.Y == 1 && Mover.Velocity.Y >= flutterSpeed) 
                                || (GravityChanger.Direction.Y == -1 && Mover.Velocity.Y <= -flutterSpeed)
                                || (GravityChanger.Direction.X == 1 && Mover.Velocity.X >= flutterSpeed) 
                                || (GravityChanger.Direction.X == -1 && Mover.Velocity.X <= -flutterSpeed)
                        )
                    ) gravity = CONSTANTS.GRAVITY * 0.3f;
            }

            if (dampenGravity) gravity *= 0.75f;

            // Check

            if (!IsFeetColliding)
            {
                if (!hadLeftGround && !didJump && !DidJumpFromSpring && (((input.X != 0 || IsRunning) && GravityChanger.Direction.Y != 0) || (input.Y != 0 || IsRunning) && GravityChanger.Direction.X != 0))
                {
                    gravityGraceTime = gravityGraceDuration;
                }
                hadLeftGround = true;
            }

            if (MyGame.Cycle % 10 == 0)
            {
                Vector2? hit = Collider.GetHitInDirection(Transform.Position, GravityChanger.Direction, noTags: new List<string>() { "No", "ContactHurt", "Enemy" });
                if (hit != null) SetLastSafePosition(hit.Value);
            }

            if (lastSafePosition != null && !Camera.IsPointInside(lastSafePosition.Position))
            {
                lastSafePosition.Entity.Destroy();
                lastSafePosition = null;
            }

            // -- ANIMATE

            // Land SFX and Visual effects
            if (IsFeetColliding)
            {

                if (!didLand)
                {
                    if ((GravityChanger.IsVertical && Mover.LastVelocity.Y.Abs() >= 60) || (GravityChanger.IsHorizontal && Mover.LastVelocity.X.Abs() >= 60))
                    {
                        Controller.Rumble(0.2f, 0.2f);
                        if (!Fade.IsFading) Factory.SfxPlayer(Entity.Scene, "Bump", Transform.Position, pitch: -0.125f, volume: 0.2f);
                        Vector2 position = Transform.Bottom;
                        if (GravityChanger.Direction.Y == 1) position = Transform.Bottom;
                        else if (GravityChanger.Direction.Y == -1) position = Transform.Top;
                        else if (GravityChanger.Direction.X == 1) position = Transform.Right;
                        else if (GravityChanger.Direction.X == -1) position = Transform.Left;
                        Factory.LandParticleEffect(Entity.Scene, position);
                    }
                    else
                    {
                        Controller.Rumble(0.125f, 0.125f);
                    }
                }

                didLand = true;
                didDoubleJump = false;
            }
            else
            {
                didLand = false;
            }

            if (((GravityChanger.IsVertical && input.X != 0) || (GravityChanger.IsHorizontal && input.Y != 0)) || (IsRunning && !IsFluttering))
            {
                if (IsRunning && !IsFluttering)
                {
                    animationState = new AnimationState("Run", 2, 5, true, 20, new List<AnimationEvent>()
                    {
                        new AnimationEvent(2, _ =>
                        {
                            if (GarbanzoQuest.Talk.Letterbox?.IsAvailable != true && !MyGame.IsTalking) Factory.SfxPlayer(Entity.Scene, "RunStep", Transform.Position, cutoffTime: 0.05f, volume: 0.25f, pitch: 1f - 0.125f);
                        }),
                        new AnimationEvent(4, _ =>
                        {
                            if (GarbanzoQuest.Talk.Letterbox?.IsAvailable != true && !MyGame.IsTalking) Factory.SfxPlayer(Entity.Scene, "RunStep", Transform.Position, cutoffTime: 0.05f, volume: 0.25f, pitch: 1f);
                        })
                    });
                }
                else
                {
                    float aSpeed = 10;
                    if ((GravityChanger.IsVertical && Mover.Velocity.X.Sign() != input.X) || (GravityChanger.IsHorizontal && Mover.Velocity.Y.Sign() != input.Y)) aSpeed = 20;
                    else Math.Max(6, (float)((float)Mover.Velocity.X.Abs() / (float)moveSpeed) * 10);

                    animationState = new AnimationState("Walk", 2, 5, true, aSpeed, new List<AnimationEvent>()
                    {
                        new AnimationEvent(3, _ =>
                        {
                            if (isOnIce)
                            {
                                Factory.SfxPlayer(Entity.Scene, "IceSlip", Transform.Position, cutoffTime: 0.05f, pitch: 0.25f);
                                Controller.Rumble(0.05f, 0.05f);
                            }
                            if (IsMoonwalking)
                            {
                                Factory.SfxPlayer(Entity.Scene, "LittleStep", Transform.Position, cutoffTime: 0.05f, pitch: -0.25f, volume: 0.15f);
                            }
                        }),
                        new AnimationEvent(5, _ =>
                        {
                            if (isOnIce)
                            {
                                Factory.SfxPlayer(Entity.Scene, "IceSlip", Transform.Position, cutoffTime: 0.05f, pitch: 0.5f);
                                Controller.Rumble(0.05f, 0.05f);
                            }
                            if (IsMoonwalking)
                            {
                                Factory.SfxPlayer(Entity.Scene, "LittleStep", Transform.Position, cutoffTime: 0.05f, pitch: -0.5f, volume: 0.15f);
                            }
                        })
                    });
                }

                if (IsFeetColliding
                    && runDustTime <= 0
                    && (
                        (GravityChanger.IsVertical && (Mover.Velocity.X.Sign() != input.X || didReleaseInputX))
                            || (GravityChanger.IsHorizontal && (Mover.Velocity.Y.Sign() != input.Y || didReleaseInputY))
                            || IsRunning
                    )
                )
                {
                    didReleaseInputX = false;
                    didReleaseInputY = false;
                    runDustTime = 0.1f;
                    lastRunDustDirection = Math.Sign(Mover.Velocity.X);

                    Vector2 position = Transform.Bottom;
                    if (GravityChanger.Direction.Y == 1) position = Transform.Bottom;
                    else if (GravityChanger.Direction.Y == -1) position = Transform.Top;
                    else if (GravityChanger.Direction.X == 1) position = Transform.Right;
                    else if (GravityChanger.Direction.X == -1) position = Transform.Left;

                    Factory.ParticleEffect(
                        scene: Entity.Scene,
                        position: position,
                        amount: Chance.Range(1, 3),
                        size: new Vector2(4, 4),
                        speed: new Range(10f, 20f),
                        vectorDirection: GravityChanger.IsVertical
                            ? new Vector2(Math.Sign(Mover.Velocity.X), Chance.Range(-0.25f, 0.25f))
                            : new Vector2(Chance.Range(-0.25f, 0.25f), Math.Sign(Mover.Velocity.Y)),
                        duration: 0.3f,
                        spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                        directionType: ParticleDirection.Vector,
                        animationState: new AnimationState("Idle", 1, 3, false, 10),
                        fadeOverTime: false,
                        additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
                   );
                }
            }
            // Idle
            else
            {
                if ((GravityChanger.IsVertical && input.Y < 0) || (GravityChanger.IsHorizontal && input.X < 0)) animationState = new AnimationState("Look", 8, 9, false);
                else if ((GravityChanger.IsVertical && input.Y > 0) || (GravityChanger.IsHorizontal && input.X > 0)) animationState = new AnimationState("Look2", 8, 9, false);
                else animationState = new AnimationState("Idle", 8, 9, false);
            }

            if (!IsFeetColliding && gravityGraceTime <= 0 && !DisableGravity)
            {
                animationState = new AnimationState("Fall", 6, 7);
            }

            if (IsFluttering)
            {
                if (Controller.Jump.IsPressed) Factory.SfxPlayer(Entity.Scene, "RunStep", Transform.Position, cutoffTime: 0.5f, volume: 0.4f, pitch: 0);
                animationState = new AnimationState("Flutter", 11, 12, true, 20, new List<AnimationEvent>()
                {
                    new AnimationEvent(11, _ => {
                        if (GarbanzoQuest.Talk.Letterbox?.IsAvailable != true && !MyGame.IsTalking) Factory.SfxPlayer(Entity.Scene, "RunStep", Transform.Position, cutoffTime: 0.05f, volume: 0.25f, pitch: IsTouchingAntiGravity ? Chance.Range(-1f, -0.5f) : Chance.Range(0.5f, 1f));
                        Controller.Rumble(0.1f, 0.1f);

                        Factory.ParticleEffect(
                            scene: Entity.Scene,
                            position: Transform.Bottom + new Vector2(0, 2),
                            amount: 1,
                            size: new Vector2(8, 4),
                            speed: new Range(50f, 70f),
                            vectorDirection: new Vector2(Chance.Range(-0.5f, 0.5f), 1),
                            duration: 0.2f,
                            spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                            directionType: ParticleDirection.Vector,
                            animationState: new AnimationState("Idle", 1, 3, false, 10),
                            fadeOverTime: false
                        );

                        Factory.ParticleEffect(
                            scene: Entity.Scene,
                            position: Transform.Bottom + new Vector2(0, 2),
                            amount: 1,
                            size: new Vector2(8, 4),
                            speed: new Range(50f, 70f),
                            vectorDirection: new Vector2(Chance.Range(-0.5f, 0.5f), 1),
                            duration: 0.2f,
                            spriteTemplate: new Sprite("MiniStar", 8, 1, IsPlayerOne ? Paint.PlayerOne : Paint.PlayerTwo),
                            directionType: ParticleDirection.Vector,
                            fadeOverTime: false
                        );
                    })
                });
            }

            int additionalOffset = 0;

            if (IsRunning && !IsFluttering)
            {
                additionalOffset = rowLength * 7;
            }

            if (GravityChanger.Direction.Y == 1)
            {
                if (input.Y < 0) additionalOffset += rowLength * 2;
                else if (input.Y > 0) additionalOffset += rowLength * 4;
            }
            else if (GravityChanger.Direction.Y == -1)
            {
                if (input.Y < 0) additionalOffset += rowLength * 4;
                else if (input.Y > 0) additionalOffset += rowLength * 2;
            }
            else if (GravityChanger.Direction.X == -1)
            {
                if (input.X > 0) additionalOffset += rowLength * 2;
                else if (input.X < 0) additionalOffset += rowLength * 4;
            }
            else if (GravityChanger.Direction.X == 1)
            {
                if (input.X > 0) additionalOffset += rowLength * 4;
                else if (input.X < 0) additionalOffset += rowLength * 2;
            }

            if (!canShoot)
            {
                Animator.Offset = rowLength;
            }
            else
            {
                Animator.Offset = 0;
            }

            Animator.Offset += additionalOffset;

            if (isBarking || homingWadTime > 0)
            {
                Animator.Offset = rowLength * 6;
            }

            if (fellOffScreen)
            {
                Animator.Offset = 0;
                if (fellOffScreenCanRecover) animationState = new AnimationState("ContactHurt", 10);
                else animationState = new AnimationState("BounceWeak", 23);
                fellRotation = fellRotation.MoveOverTime((MathF.Max(1f, MathF.Min(10f, Mover.Velocity.X.Abs() * (fellOffScreenCanRecover ? 1f : 100f)))) * (Mover.Velocity.X > 0 ? 1 : -1), fellOffScreenCanRecover ? 0.5f : 0.001f);
                if (Liver.Health > 0) Sprite.Rotation += fellRotation * Time.Delta;
                else Sprite.Rotation = GravityChanger.Rotation;
            }

            // Apply velocity

            if (IsFeetColliding)
            {
                if (fellOffScreen)
                {
                    State = "Leap";
                    ExitFallOffScreen();
                }
                lockRunDirection = Vector2.Zero;
                fellOffScreenAndInAir = false;
                fellOffScreenCount = 1;
                hadLeftGround = false;
                didJump = false;
                DidJumpFromSpring = false;
                if (State == "Leap") return;
            }

            dir = Sprite.FlipHorizontally ? -1 : 1;
            moveDir = (GravityChanger.IsVertical ? input.X : input.Y);
            if (IsRunning && moveDir == 0) moveDir = (GravityChanger.Direction.Y == -1 || GravityChanger.Direction.X == 1 ? -dir : dir);

            if (lockRunDirection != Vector2.Zero)
            {
                if (input.X != 0) lockRunDirection.Y = 0;
                if (input.Y != 0) lockRunDirection.X = 0;

                if (GravityChanger.IsVertical && lockRunDirection.Y != 0)
                {
                    moveDir = 0;
                }

                if (GravityChanger.IsHorizontal && lockRunDirection.X != 0)
                {
                    moveDir = 0;
                }
            }

            float desiredVelocity = moveDir * (IsRunning ? runSpeed : moveSpeed);
            if (slowedDown) desiredVelocity = moveDir * moveSpeed * 0.7f;

            // Get Acceleration

            float acceleration = MathF.Abs(desiredVelocity) >= MathF.Abs(GravityChanger.IsVertical ? Mover.Velocity.X : Mover.Velocity.Y) ? upAcceleration : downAcceleration;

            if (!IsFeetColliding)
            {
                if (((GravityChanger.IsVertical && input.X == 0) || (GravityChanger.IsHorizontal && input.Y == 0)) && !IsRunning) acceleration = 1;
                else if (!fellOffScreenCanRecover) acceleration = 0.2f;
                else if (fellOffScreen && Mover.Velocity.Y < 0) acceleration = 0.125f;
                else acceleration = airAcceleration;
            }

            if (IsTouchingAntiGravity) acceleration = acceleration * 5f;
            if (isOnIce) acceleration = iceAcceleration;
            if (IsRunning) acceleration = 0;

            acceleration = MathF.Min(1, acceleration);

            // Walk

            if (GravityChanger.IsVertical)
            {
                if (!(
                    Mover.Velocity.X.Abs() > desiredVelocity.Abs() 
                        && (input.X == Math.Sign(Mover.Velocity.X) || (IsRunning && dir == desiredVelocity.Sign()))
                        && !IsFeetColliding
                ))
                {
                    Mover.Velocity = new Vector2(
                        Mover.Velocity.X.MoveOverTime(desiredVelocity, acceleration, min: 1),
                        Mover.Velocity.Y
                    );
                }
            }
            else if (GravityChanger.IsHorizontal)
            {
                if (!(
                    Mover.Velocity.Y.Abs() > desiredVelocity.Abs() 
                        && (input.Y == Math.Sign(Mover.Velocity.Y) || (IsRunning && dir == desiredVelocity.Sign()))
                        && !IsFeetColliding
                ))
                {
                    Mover.Velocity = new Vector2(
                        Mover.Velocity.X,
                        Mover.Velocity.Y.MoveOverTime(desiredVelocity, acceleration, min: 1)
                    );
                }
            }

            // Bumped Head
            if (IsHeadColliding)
            {
                if (GravityChanger.Direction.Y == 1)
                {
                    if (IsTouchingAntiGravity) Mover.Velocity.Y = CONSTANTS.MAX_FALL_SPEED / 4f;
                    else if (fellOffScreenAndInAir) CheckPhaseThrough();
                    else Mover.Velocity.Y = 0;

                    Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.25f, 0.25f), volume: 0.5f);
                    Factory.DustParticleEffect(position: Transform.Top);
                }
                else if (GravityChanger.Direction.Y == -1)
                {
                    if (IsTouchingAntiGravity) Mover.Velocity.Y = -CONSTANTS.MAX_FALL_SPEED / 4f;
                    else if (fellOffScreenAndInAir) CheckPhaseThrough();
                    else Mover.Velocity.Y = 0;

                    Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.25f, 0.25f), volume: 0.5f);
                    Factory.DustParticleEffect(position: Transform.Bottom);
                }
                else if (GravityChanger.Direction.X == 1)
                {
                    if (IsTouchingAntiGravity) Mover.Velocity.X = CONSTANTS.MAX_FALL_SPEED / 4f;
                    else if (fellOffScreenAndInAir) CheckPhaseThrough();
                    else Mover.Velocity.X = 0;

                    Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.25f, 0.25f), volume: 0.5f);
                    Factory.DustParticleEffect(position: Transform.Left);
                }
                else if (GravityChanger.Direction.X == -1)
                {
                    if (IsTouchingAntiGravity) Mover.Velocity.X = -CONSTANTS.MAX_FALL_SPEED / 4f;
                    else if (fellOffScreenAndInAir) CheckPhaseThrough();
                    else Mover.Velocity.X = 0;

                    Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.25f, 0.25f), volume: 0.5f);
                    Factory.DustParticleEffect(position: Transform.Right);
                }
            }

            // Apply Gravity
            if (IsFeetColliding)
            {
                if (gravityGraceTime <= 0)
                {
                    if (GravityChanger.Direction.Y == 1) Mover.Velocity.Y = 0.1f;
                    else if (GravityChanger.Direction.Y == -1) Mover.Velocity.Y = -0.1f;
                    else if (GravityChanger.Direction.X == 1) Mover.Velocity.X = 0.1f;
                    else if (GravityChanger.Direction.X == -1) Mover.Velocity.X = -0.1f;
                }
            }
            else if ((gravityGraceTime <= 0 || ((input.X == 0 && GravityChanger.IsVertical) || (input.Y == 0 && GravityChanger.IsHorizontal))) && !DisableGravity)
            {
                if (GravityChanger.Direction.Y == 1) Mover.Velocity.Y += (gravity * (changedGravityTime > 0 ? 0.25f : 1f)) * Time.ModifiedDelta;
                else if (GravityChanger.Direction.Y == -1) Mover.Velocity.Y -= (gravity * (changedGravityTime > 0 ? 0.25f : 1f)) * Time.ModifiedDelta;
                else if (GravityChanger.Direction.X == 1) Mover.Velocity.X += (gravity * (changedGravityTime > 0 ? 0.25f : 1f)) * Time.ModifiedDelta;
                else if (GravityChanger.Direction.X == -1) Mover.Velocity.X -= (gravity * (changedGravityTime > 0 ? 0.25f : 1f)) * Time.ModifiedDelta;

                // Flutter
                if (IsFluttering)
                {
                    // float tempFlutterSpeed = flutterFallSpeed;
                    // if (IsTouchingAntiGravity) tempFlutterSpeed = flutterFallSpeed * 0.5f;
                    // if (GravityChanger.GravityDirection.Y == 1) Mover.Velocity.Y =  tempFlutterSpeed;
                    // else if (GravityChanger.GravityDirection.Y == -1) Mover.Velocity.Y =  -tempFlutterSpeed;
                    // else if (GravityChanger.GravityDirection.X == 1) Mover.Velocity.X =  tempFlutterSpeed;
                    // else if (GravityChanger.GravityDirection.X == -1) Mover.Velocity.X =  -tempFlutterSpeed;
                    // if (GravityChanger.GravityDirection.Y == 1) Mover.Velocity.Y = Math.Min(Mover.Velocity.Y, tempFlutterSpeed);
                    // else if (GravityChanger.GravityDirection.Y == -1) Mover.Velocity.Y = Math.Max(Mover.Velocity.Y, -tempFlutterSpeed);
                    // else if (GravityChanger.GravityDirection.X == 1) Mover.Velocity.X = Math.Min(Mover.Velocity.X, tempFlutterSpeed);
                    // else if (GravityChanger.GravityDirection.X == -1) Mover.Velocity.X = Math.Max(Mover.Velocity.X, -tempFlutterSpeed);
                }
            }

            if (DisableGravity || gravityGraceTime > 0)
            {
                if ((GravityChanger.Direction.Y == 1 && Mover.Velocity.Y > 0) || (GravityChanger.Direction.Y == -1 && Mover.Velocity.Y < 0))
                {
                    Mover.Velocity.Y = 0;
                }

                if ((GravityChanger.Direction.X == 1 && Mover.Velocity.X > 0) || (GravityChanger.Direction.X == -1 && Mover.Velocity.X < 0))
                {
                    Mover.Velocity.X = 0;
                }
            }

            if (!IsInControl) return;

            // Flip
            if (!IsMoonwalking && !fellOffScreen)
            {
                if (GravityChanger.Direction.Y == 1)
                {
                    if (input.X < 0) Sprite.FlipHorizontally = true;
                    if (input.X > 0) Sprite.FlipHorizontally = false;
                }
                else if (GravityChanger.Direction.Y == -1)
                {
                    if (input.X < 0) Sprite.FlipHorizontally = false;
                    if (input.X > 0) Sprite.FlipHorizontally = true;
                }
                else if (GravityChanger.Direction.X == -1)
                {
                    if (input.Y < 0) Sprite.FlipHorizontally = true;
                    if (input.Y > 0) Sprite.FlipHorizontally = false;
                }
                else if (GravityChanger.Direction.X == 1)
                {
                    if (input.Y < 0) Sprite.FlipHorizontally = false;
                    if (input.Y > 0) Sprite.FlipHorizontally = true;
                }
            }

            // Jump

            jumpBuffer -= Time.Delta;

            if (!(IsFeetColliding || gravityGraceTime > 0) && Controller.Jump.IsPressed)
            {
                jumpBuffer = jumpBufferDuration;
            }

            flutterBuffer -= Time.Delta;

            if (!(IsFeetColliding || gravityGraceTime > 0) && BadgeIsPressed(Badge.Float))
            {
                flutterBuffer = flutterBufferDuration;
            }

            if ((IsFeetColliding || gravityGraceTime > 0 || ((SaveData.Current?.HasDoubleJump == true || (FilterBadges.Enabled && FilterBadges.DoubleJump)) && !IsFeetColliding && !didDoubleJump && !IsFluttering && MyGame.CurrentZone != "Title") && !IsTouchingAntiGravity) && (Controller.Jump.IsPressed || jumpBuffer > 0))
            {
                StatListener.IncrementJumps();

                if (!(IsFeetColliding || gravityGraceTime > 0))
                {
                    didDoubleJump = true;
                    DenyNoBadge = MyGame.CurrentZone;

                    fellOffScreen = false;
                    fellOffScreenAndInAir = false;
                    fellRotation = 0;
                    fellOffScreenCanRecover = true;
                }

                gravityGraceTime = 0;

                float pitch = Chance.Range(-0.25f, -0.1f);
                if (IsTouchingAntiGravity) pitch = Chance.Range(-1f, -0.75f);
                if (didDoubleJump) pitch = Chance.Range(0.25f, 0.5f);

                Mover.Velocity.V(jumpSpeed * (didDoubleJump ? 0.55f : 1f));

                if (slowedDown)
                {
                    if (GravityChanger.IsVertical) Mover.Velocity.Y *= 0.8f;
                    else if (GravityChanger.IsHorizontal) Mover.Velocity.X *= 0.8f;
                }
                didJump = true;

                Factory.SfxPlayer(name: "Jump", position: Transform.Position, pitch: pitch, parent: Entity, onlyOne: false);
                Factory.ParticleEffect(
                    scene: Entity.Scene,
                    position: Transform.Position + new Vector2(-4, 4),
                    amount: Chance.Range(1, 3),
                    size: new Vector2(4, 4),
                    speed: new Range(10f, 20f),
                    vectorDirection: new Vector2(-1, Chance.Range(-0.25f, 0.25f)),
                    duration: 0.3f,
                    spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                    directionType: ParticleDirection.Vector,
                    animationState: new AnimationState("Idle", 1, 3, false, 10),
                    fadeOverTime: false,
                    additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
                );
                Factory.ParticleEffect(
                   scene: Entity.Scene,
                   position: Transform.Position + new Vector2(4, 4),
                   amount: Chance.Range(1, 3),
                   size: new Vector2(4, 4),
                   speed: new Range(10f, 20f),
                   vectorDirection: new Vector2(1, Chance.Range(-0.25f, 0.25f)),
                   duration: 0.3f,
                   spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                   directionType: ParticleDirection.Vector,
                   animationState: new AnimationState("Idle", 1, 3, false, 10),
                   fadeOverTime: false,
                   additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
               );
                Factory.ParticleEffect(
                    scene: Entity.Scene,
                    position: Transform.Position + new Vector2(0, 4),
                    amount: Chance.Range(1, 4),
                    size: new Vector2(4, 4),
                    speed: new Range(50f, 60f),
                    vectorDirection: new Vector2(Chance.Range(-0.25f, 0.25f), -1),
                    duration: 0.3f,
                    spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                    directionType: ParticleDirection.Vector,
                    animationState: new AnimationState("Idle", 1, 3, false, 10),
                    fadeOverTime: false,
                    additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
                );
            }

            // Min jump
            if (!didForceJump && Controller.Jump.IsReleased && !DidJumpFromSpring)
            {
                if (GravityChanger.Direction.Y == 1 && Mover.Velocity.Y < minJumpSpeed) Mover.Velocity.Y = minJumpSpeed;
                else if (GravityChanger.Direction.Y == -1 && Mover.Velocity.Y > -minJumpSpeed) Mover.Velocity.Y = -minJumpSpeed;
                else if (GravityChanger.Direction.X == 1 && Mover.Velocity.X < minJumpSpeed) Mover.Velocity.X = minJumpSpeed;
                else if (GravityChanger.Direction.X == -1 && Mover.Velocity.X > -minJumpSpeed) Mover.Velocity.X = -minJumpSpeed;
            }

            // Spit
            HandleSpit();
            HandleBark();

            // -- BADGES

            // Float Jump
            if (IsFluttering)
            {
                if (Collider.Info.Down && GravityChanger.Direction.Y == 1) IsFluttering = false;
                if (Collider.Info.Up && GravityChanger.Direction.Y == -1) IsFluttering = false;
                if (Collider.Info.Right && GravityChanger.Direction.X == 1) IsFluttering = false;
                if (Collider.Info.Left && GravityChanger.Direction.X == -1) IsFluttering = false;
            }
            else
            {
                if ((BadgeIsPressed(Badge.Float) || flutterBuffer > 0) && fellOffScreenCanRecover)
                {
                    Flutter();
                }
            }

            // Leap
            if (BadgeIsPressed(Badge.Leap) && fellOffScreenCanRecover) LeapStart();

            // Bounce
            if (BadgeIsPressed(Badge.Bounce) && canEnterBounce && fellOffScreenCanRecover) BounceStart();
        }

        void NormalExit(string state)
        {
            gravity = CONSTANTS.GRAVITY;
            jumpBuffer = 0;
            gravityGraceTime = 0;
            IsFluttering = false;
            fellOffScreenAndInAir = false;
            isBarking = false;
            barkCount = 0;

            if (state != "Swim")
            {
                ExitFallOffScreen();
            }
            else
            {
                fellOffScreen = false;
                fellOffScreenCanRecover = true;
            }
        }

        // Swim

        void ExitFromWater(bool effect = true)
        {
            if (effect)
            {
                Factory.SfxPlayer(Entity.Scene, "WaterExit");
                Factory.WaterParticleEffect(Entity.Scene, Transform.Position, Mover, Shaker, false);
            }

            if (GravityChanger.Direction.Y == 1)
            {
                Sprite.FlipHorizontally = Sprite.FlipVertically;
                Sprite.FlipVertically = false;
                Sprite.Rotation = GravityChanger.Rotation;
            }
            else if (GravityChanger.Direction.Y == -1)
            {
                Sprite.FlipHorizontally = Sprite.FlipVertically;
                Sprite.FlipVertically = false;
                Sprite.Rotation = MathF.PI;
            }
            else if (GravityChanger.Direction.X == 1)
            {
                Sprite.FlipHorizontally = Sprite.FlipVertically;
                Sprite.FlipVertically = false;
                Sprite.Rotation = -MathF.PI / 2;
            }
            else if (GravityChanger.Direction.X == -1)
            {
                Sprite.FlipHorizontally = Sprite.FlipVertically;
                Sprite.FlipVertically = false;
                Sprite.Rotation = MathF.PI / 2;
            }

            lockRunDirection = SwimInput;
        }

        void JumpFromWater(Vector2 input)
        {
            animationState = new AnimationState("Fall", 6, 7);
            Sprite.TileNumber = 6;
            Animator.SetState(animationState);

            // Down
            if (((!Collider.IsPassenger || !Collider.Info.Down) || input.Y <= 0) 
                && GravityChanger.Direction.Y == 1
                && input.Y <= 0
                && (lastWaterInput.Y < 0 || !Collider.IsTouchingWater(point: Transform.Position - (GravityChanger.Direction * (Transform.Height + 4f))))
            )
            {
                Mover.Velocity.Y = jumpFromWaterSpeed;
                gravity = CONSTANTS.GRAVITY;
                didJumpFromWater = true;
            }
            // Up
            else if (((!Collider.IsPassenger || !Collider.Info.Up) || input.Y >= 0)
                && GravityChanger.Direction.Y == -1
                && input.Y >= 0
                && (lastWaterInput.Y > 0 || !Collider.IsTouchingWater(point: Transform.Position + (GravityChanger.Direction * (Transform.Height + 4f))))
            )
            {
                Mover.Velocity.Y = -jumpFromWaterSpeed;
                gravity = CONSTANTS.GRAVITY;
                didJumpFromWater = true;
            }
            // Right
            else if (((!Collider.IsPassenger || !Collider.Info.Right) || input.X <= 0) 
                && GravityChanger.Direction.X == 1
                && input.X <= 0
                && (lastWaterInput.X < 0 || !Collider.IsTouchingWater(point: Transform.Position - (GravityChanger.Direction * (Transform.Width + 4f))))
            )
            {
                Mover.Velocity.X = jumpFromWaterSpeed;
                gravity = CONSTANTS.GRAVITY;
                didJumpFromWater = true;
            }
            // Left
            else if (((!Collider.IsPassenger || !Collider.Info.Left) || input.X >= 0) 
                && GravityChanger.Direction.X == -1
                && input.X >= 0
                && (lastWaterInput.X > 0 || !Collider.IsTouchingWater(point: Transform.Position + (GravityChanger.Direction * (Transform.Width + 4f))))
            )
            {
                Mover.Velocity.X = -jumpFromWaterSpeed;
                gravity = CONSTANTS.GRAVITY;
                didJumpFromWater = true;
            }

            NormalStart();
        }

        public void SwimStart()
        {
            lockRunDirection = Vector2.Zero;
            didJump = true;
            didDoubleJump = false;
            fellOffScreen = false;
            fellOffScreenAndInAir = false;
            fellOffScreenCount = 1;

            if (State != "Recoil" && State != "Dash" && State != "PingPong")
            {
                float angle = 0;

                if (GravityChanger.Name == "Down")
                {
                    lastWaterInput.X = Sprite.FlipHorizontally ? -1 : 1;
                    SwimDirection = lastWaterInput.X == -1 ? Direction.Left : Direction.Right;
                    angle = MathF.Atan2(0, Sprite.FlipHorizontally ? -1 : 1);
                    if (Sprite.FlipHorizontally) Sprite.FlipVertically = true;
                    else Sprite.FlipVertically = false;
                }
                else if (GravityChanger.Name == "Up")
                {
                    lastWaterInput.X = Sprite.FlipHorizontally ? 1 : -1;
                    SwimDirection = lastWaterInput.X == -1 ? Direction.Left : Direction.Right;
                    angle = MathF.Atan2(0, Sprite.FlipHorizontally ? 1 : -1);
                    if (Sprite.FlipHorizontally) Sprite.FlipVertically = true;
                    else Sprite.FlipVertically = false;
                }
                else if (GravityChanger.Name == "Left")
                {
                    lastWaterInput.Y = Sprite.FlipHorizontally ? -1 : 1;
                    SwimDirection = lastWaterInput.Y == -1 ? Direction.Up : Direction.Down;
                    angle = MathF.Atan2(Sprite.FlipHorizontally ? -1 : 1, 0);
                    if (Sprite.FlipHorizontally) Sprite.FlipVertically = true;
                    else Sprite.FlipVertically = false;
                }
                else if (GravityChanger.Name == "Right")
                {
                    lastWaterInput.Y = Sprite.FlipHorizontally ? 1 : -1;
                    SwimDirection = lastWaterInput.Y == -1 ? Direction.Up : Direction.Down;
                    angle = MathF.Atan2(Sprite.FlipHorizontally ? 1 : -1, 0);
                    if (Sprite.FlipHorizontally) Sprite.FlipVertically = true;
                    else Sprite.FlipVertically = false;
                }
                
                Sprite.Rotation = angle;
            }

            if (State != "Recoil" && State != "Talk" && State != "Dash" && State != "PingPong" && State != "Parry")
            {
                Factory.SfxPlayer(Entity.Scene, "WaterEnter");
                Factory.WaterParticleEffect(Entity.Scene, Transform.Position, Mover, Shaker, true);
            }

            ChangeState("Swim");
            Sprite.FlipHorizontally = false;
            Animator.Offset = 0;
            Mover.Velocity.Y *= 0.5f;
            didForceJump = false;
            dampenGravity = false;
            didJumpFromWater = false;
            gravityGraceTime = 0f;
        }

        void Swim()
        {
            if (BadgeIsPressed(Badge.Parry))
            {
                if (HurtHandler.IsInvincible)
                {
                    Factory.SfxPlayer(name: "NuUh");
                    Shaker.ShakeY();
                    Factory.FadingText(position: Transform.Top + new Vector2(0, -8) + Utility.Shake(8), text: Dial.Key("NOPE"), color: Paint.Red, outlineColor: Paint.Black);
                }
                else
                {
                    ParryStart();
                    return;
                }
            }

            bool isTouchingWaterfall = Collider.IsTouchingWaterfall();

            int additionalOffset = 0;

            if (!canShoot)
            {
                additionalOffset = rowLength;
            }

            bool bubble = true;
            // Idle
            animationState = new AnimationState("Swim", 37, 40, true, 4, new List<AnimationEvent>()
            {
                new AnimationEvent(38, _ =>
                {
                    if (bubble)
                    {
                        Factory.BubbleParticleEffect(Entity.Scene, Transform.Position);
                    }

                    bubble = !bubble;
                })
            });

            SwimInput = input.Normalized();

            if (IsRunning && SwimInput == Vector2.Zero)
            {
                if (SwimDirection == Direction.Up) SwimInput.Y = -1;
                if (SwimDirection == Direction.Down) SwimInput.Y = 1;
                if (SwimDirection == Direction.Left) SwimInput.X = -1;
                if (SwimDirection == Direction.Right) SwimInput.X = 1;
            }

            if (SwimInput.X != 0) lastWaterInput.X = SwimInput.X;
            if (SwimInput.Y != 0) lastWaterInput.Y = SwimInput.Y;

            if (!IsMoonwalking)
            {
                if (
                    (SwimInput.Y != 0 && SwimInput.X == 0) 
                        || (SwimDirection == Direction.Up && SwimInput.Y > 0) 
                        || (SwimDirection == Direction.Down && SwimInput.Y < 0)
                )
                {
                    if (SwimInput.Y < 0) SwimDirection = Direction.Up;
                    if (SwimInput.Y > 0) SwimDirection = Direction.Down;
                }

                if (
                    (SwimInput.Y == 0 && SwimInput.X != 0) 
                        || (SwimDirection == Direction.Left && SwimInput.X > 0) 
                        || (SwimDirection == Direction.Right && SwimInput.X < 0)
                )
                {
                    if (SwimInput.X < 0) SwimDirection = Direction.Left;
                    if (SwimInput.X > 0) SwimDirection = Direction.Right;
                }
            }

            if (SwimDirection == Direction.Up) Sprite.Rotation = MathF.Atan2(-1, 0);
            if (SwimDirection == Direction.Down) Sprite.Rotation = MathF.Atan2(1, 0);
            if (SwimDirection == Direction.Left) Sprite.Rotation = MathF.Atan2(0, -1);
            if (SwimDirection == Direction.Right) Sprite.Rotation = MathF.Atan2(0, 1);

            if (!IsTouchingWater)
            {
                JumpFromWater(SwimInput);
                return;
            }

            if (IsRunning && !PlayerCornerChecker.DidNudge)
            {
                if (Collider.Info.Left && SwimDirection == Direction.Left)
                {
                    runningBump += Time.ModifiedDelta;
                    if (runningBump >= runningBumpDuration)
                    {
                        RecoilStart(duration: 0.4f, velocity: new Vector2(100, 0));
                        Factory.SfxPlayer(name: "Bump", pitch: Chance.Range(-0.5f, 0f), position: Transform.Position);
                        runningBump = 0;
                    }
                }
                else if (Collider.Info.Right && SwimDirection == Direction.Right)
                {
                    runningBump += Time.ModifiedDelta;
                    if (runningBump >= runningBumpDuration)
                    {
                        RecoilStart(duration: 0.4f, velocity: new Vector2(-100, 0));
                        Factory.SfxPlayer(name: "Bump", pitch: Chance.Range(-0.5f, 0f), position: Transform.Position);
                        runningBump = 0;
                    }
                }
                else if (Collider.Info.Up && SwimDirection == Direction.Up)
                {
                    runningBump += Time.ModifiedDelta;
                    if (runningBump >= runningBumpDuration)
                    {
                        RecoilStart(duration: 0.4f, velocity: new Vector2(0, 100));
                        Factory.SfxPlayer(name: "Bump", pitch: Chance.Range(-0.5f, 0f), position: Transform.Position);
                        runningBump = 0;
                    }
                }
                else if (Collider.Info.Down && SwimDirection == Direction.Down)
                {
                    runningBump += Time.ModifiedDelta;
                    if (runningBump >= runningBumpDuration)
                    {
                        RecoilStart(duration: 0.4f, velocity: new Vector2(0, -100));
                        Factory.SfxPlayer(name: "Bump", pitch: Chance.Range(-0.5f, 0f), position: Transform.Position);
                        runningBump = 0;
                    }
                }
                else
                {
                    runningBump = 0;
                }
            }

            if (SwimInput.X != 0 || SwimInput.Y != 0)
            {
                animationState = animationSwimGo;

                if (!IsMoonwalking && (SwimInput.X != 0 || SwimInput.Y != 0))
                {
                    if (GravityChanger.Name == "Down")
                    {
                        if (lastWaterInput.X < 0) Sprite.FlipVertically = true;
                        else if (lastWaterInput.X > 0) Sprite.FlipVertically = false;
                    }
                    else if (GravityChanger.Name == "Up")
                    {
                        if (lastWaterInput.X < 0) Sprite.FlipVertically = false;
                        else if (lastWaterInput.X > 0) Sprite.FlipVertically = true;
                    }
                    else if (GravityChanger.Name == "Left")
                    {
                        if (lastWaterInput.Y < 0) Sprite.FlipVertically = true;
                        else if (lastWaterInput.Y > 0) Sprite.FlipVertically = false;
                    }
                    else if (GravityChanger.Name == "Right")
                    {
                        if (lastWaterInput.Y < 0) Sprite.FlipVertically = false;
                        else if (lastWaterInput.Y > 0) Sprite.FlipVertically = true;
                    }
                }
            }

            if (slowedDown && animationState.Name == "SwimGo")
            {
                Animator.Offset = rowLength * 7;
            }
            else
            {
                Animator.Offset = 0;
            }

            if (IsRunning)
            {
                Animator.Offset = rowLength * 7;
            }

            Animator.Offset += additionalOffset;

            if (isBarking)
            {
                Animator.Offset = rowLength * 6;
            }

            Mover.Velocity = Mover.Velocity.MoveOverTime(
                SwimInput * (
                    swimSpeed
                        + (IsRunning ? (runSpeed - moveSpeed) : 0)
                    )
                    * (slowedDown ? 0.5f : 1f),
                swimAcceleration
            );

            if (isTouchingWaterfall && !PlayerCornerChecker.OverrideWaterfall)
            {
                if (SwimInput.Y < 0) Mover.Velocity.Y = -swimSpeed + (swimSpeed * 0.5f) - (IsRunning ? (runSpeed - moveSpeed) : 0);
                else if (SwimInput.Y > 0) Mover.Velocity.Y = swimSpeed + (swimSpeed * 0.5f) + (IsRunning ? (runSpeed - moveSpeed) : 0);
                else Mover.Velocity.Y = swimSpeed * 0.5f;
            }

            bool stopHorizontal = !PlayerCornerChecker.DidNudge && ((Collider.Info.Left && Mover.Velocity.X < 0) || (Collider.Info.Right && Mover.Velocity.X > 0));
            bool stopVertical = !PlayerCornerChecker.DidNudge && ((Collider.Info.Up && Mover.Velocity.Y < 0) || (Collider.Info.Down && Mover.Velocity.Y > 0));
            if (stopHorizontal)
            {
                if (stopGrace > 2) Mover.Velocity.X = 0.1f * Mover.Velocity.X.Sign();
                stopGrace++;
            }
            if (stopVertical)
            {
                if (stopGrace > 2) Mover.Velocity.Y = 0.1f * Mover.Velocity.Y.Sign();
                stopGrace++;
            }
            if (!stopHorizontal && !stopVertical) stopGrace = 0;

            HandleSpit();
            HandleBark();
            wasTouchingWaterfall = isTouchingWaterfall;

            if (BadgeIsPressed(Badge.Leap))
            {
                DashStart();
            }
            else if (BadgeIsPressed(Badge.Bounce))
            {
                PingPongStart();
            }
        }

        void SwimExit(string state)
        {
            if (state != "Normal") isBarking = false;

            if (state != "Dash" && state != "PingPong")
            {
                lastWaterInput = Vector2.Zero;
            }

            if (state != "Recoil" && state != "Talk" && state != "Dash" && state != "PingPong")
            {
                ExitFromWater(state != "Parry");
            }
        }

        // Recoil

        void RecoilStart(Transform hurter = null, float? duration = null, Vector2? velocity = null)
        {
            isBarking = false;
            IsRunning = false;

            recoilReturnState = State;
            ChangeState("Recoil");

            Collider.Info.BakedDown = false;

            recoilTime = duration ?? recoilDuration;
            if (velocity == null)
            {
                recoilVelocity = Mover.Velocity;
                Mover.Velocity = Vector2.Zero;
            }
            else
            {
                recoilVelocity = null;
                Mover.Velocity = velocity.Value;
            }

            // if (hurter?.Entity?.Tags?.Contains("Pain") == true)
            // {
            //     recoilVelocity = new Vector2(0, 1);
            //     Mover.Velocity = Vector2.Zero;
            // }

            if (recoilReturnState == "Pants")
            {
                Animator.Offset = rowLength * 6;
            }
            else
            {
                Animator.Offset = 0;
            }
        }

        void Recoil()
        {
            if (IsTouchingWater && recoilReturnState != "Swim" && recoilReturnState != "Stop" && recoilReturnState != "Dash" && recoilReturnState != "PingPong" && recoilReturnState != "Parry")
            {
                SwimStart();
                return;
            }

            if (recoilVelocity == null)
            {
                if (recoilReturnState == "Swim" || recoilReturnState == "Stop" || recoilReturnState == "Dash" || recoilReturnState == "PingPong") Mover.Velocity = Mover.Velocity.MoveTowardsWithSpeed(Vector2.Zero, 100f);
                else Mover.Velocity.V_Add(CONSTANTS.GRAVITY * Time.ModifiedDelta, true);
            }

            Mover.Velocity = Mover.Velocity.MoveOverTime(Vector2.Zero, 0.1f);

            Animator.Offset = 0;
            recoilTime -= Time.ModifiedDelta;

            if (recoilReturnState == "Stop")
            {
                animationState = new AnimationState("ContactHurt", 10);
                StopStart();
            }
            else
            {
                animationState = new AnimationState("ContactHurt", 10);

                if (recoilTime <= 0)
                {
                    if (IsTouchingWater)
                    {
                        SwimStart();
                    }
                    else
                    {
                        if (recoilVelocity != null)
                        {
                            Mover.Velocity = recoilVelocity.Value;
                        }
                        hadLeftGround = true;
                        NormalStart();
                    }
                }
            }
        }

        void RecoilExit(string state)
        {

        }

        // Ball

        public void BallStart()
        {
            ChangeState("Ball");

            Animator.Offset = 0;
            animationState = new AnimationState(
                name: "Ball",
                start: 48
            );
        }

        int ballBounceCount = 0;
        void Ball()
        {
            Mover.Velocity.Y += gravity * Time.ModifiedDelta;
            if (Collider.Info.Down)
            {
                if (ballBounceCount == 0)
                {
                    Mover.Velocity.Y = jumpSpeed * 0.5f;
                }
                else if (ballBounceCount == 1)
                {
                    Mover.Velocity.Y = jumpSpeed * 0.125f;
                }
                else
                {
                    Mover.Velocity.Y = 0;
                }

                ballBounceCount++;
            }
        }

        void BallExit(string exit)
        {
            ballBounceCount = 0;
        }

        // Lerp

        public void LerpStart(Entity warp)
        {
            ChangeState("Lerp");
            Factory.SfxPlayer(name: "Dash", position: Transform.Position);
            this.warp = warp;
            lerpTarget = warp.GetComponent<Transform>().Position;
        }

        void Lerp()
        {
            Transform.Position = Transform.Position.MoveTowardsWithSpeed(lerpTarget, 150f);
            Sprite.Rotation += 25 * Time.ModifiedDelta;
            if ((Transform.Position.X - lerpTarget.X).Abs() < 4 && (Transform.Position.Y - lerpTarget.Y).Abs() < 4)
            {
                warp.GetComponent<Shaker>().ShakeX();
                Shatterer.FragmentSize = 4;
                Shatterer.Shatter();
                Entity.Destroy();
                Factory.SfxPlayer(name: "Jump", pitch: 1f, onlyOne: false);
            }
        }

        void LerpExit(string exit)
        {
            lerpTarget = Vector2.Zero;
        }

        // Leap

        void LeapStart(bool invincible = true, int dir = 0)
        {
            ChangeState("Leap");

            Controller.Rumble(0.25f, 0.25f);

            dir = facingDirection;

            Factory.SfxPlayer(name: "Dash", position: Transform.Position, parent: Entity);
            animationState = new AnimationState("LeapAir", 13, 15, false);
            HurtHandler.Weaknesses = new List<string>() { };
            // HurtHandler.Weaknesses = new List<string>() { "Pain" };
            float desiredVelocity = dir * leapSpeed * (IsRunning ? 1.5f : 1f);
            if (desiredVelocity.Abs() > Mover.Velocity.H().Abs()) Mover.Velocity.H(desiredVelocity);

            Mover.Velocity.V(leapJumpSpeed);
            if (slowedDown) Mover.Velocity *= 0.5f;

            if (IsRunning) lockRunEffects = true;
            else disableRunEffects = true;

            Animator.Offset = 0;

            DidJumpFromSpring = false;
            canExitLeap = false;
            Factory.OneShotTimer(end: (_) =>
            {
                canExitLeap = true;
            }, duration: 0.1f);

            Factory.ParticleEffect(
                scene: Entity.Scene,
                position: Transform.Position + new Vector2(-4, 4),
                amount: Chance.Range(1, 3),
                size: new Vector2(4, 4),
                speed: new Range(10f, 20f),
                vectorDirection: new Vector2(-1, Chance.Range(-0.25f, 0.25f)),
                duration: 0.3f,
                spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                directionType: ParticleDirection.Vector,
                animationState: new AnimationState("Idle", 1, 3, false, 10),
                fadeOverTime: false,
                additionalVelocity: new Vector2(Mover.Velocity.X * 0.5f, 0)
            );
            Factory.ParticleEffect(
               scene: Entity.Scene,
               position: Transform.Position + new Vector2(4, 4),
               amount: Chance.Range(1, 3),
               size: new Vector2(4, 4),
               speed: new Range(10f, 20f),
               vectorDirection: new Vector2(1, Chance.Range(-0.25f, 0.25f)),
               duration: 0.3f,
               spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
               directionType: ParticleDirection.Vector,
               animationState: new AnimationState("Idle", 1, 3, false, 10),
               fadeOverTime: false,
               additionalVelocity: new Vector2(Mover.Velocity.X * 0.5f, 0)
           );
            Factory.ParticleEffect(
                scene: Entity.Scene,
                position: Transform.Position + new Vector2(0, 4),
                amount: Chance.Range(1, 4),
                size: new Vector2(4, 4),
                speed: new Range(50f, 60f),
                vectorDirection: new Vector2(Chance.Range(-0.25f, 0.25f), -1),
                duration: 0.3f,
                spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                directionType: ParticleDirection.Vector,
                animationState: new AnimationState("Idle", 1, 3, false, 10),
                fadeOverTime: false,
                additionalVelocity: new Vector2(Mover.Velocity.X * 0.5f, 0)
            );
        }

        void Leap()
        {
            if (IsTouchingWater)
            {
                HurtHandler.TriggerInvincibility(dashDuration * 0.5f);
                SwimStart();
                return;
            }

            if (!IsFeetColliding && didLandFromLeap && leapCooldownTime <= 0)
            {
                NormalStart();
                hadLeftGround = true;
                return;
            }

            if (DidJumpFromSpring)
            {
                NormalStart();
                return;
            }

            if (IsTouchingAntiGravity)
            {
                // gravity = CONSTANTS.GRAVITY * 0.5f;
            }
            else
            {
                gravity = CONSTANTS.GRAVITY;
            }

            leapCooldownTime -= Time.ModifiedDelta;
            Mover.Velocity += GravityChanger.Direction * gravity * Time.ModifiedDelta;

            if (IsHeadColliding) Mover.Velocity.V(0);

            if (IsFeetColliding)
            {
                lockRunEffects = false;
                disableRunEffects = false;

                if (!didRecoverFromLeap)
                {
                    HurtHandler.Weaknesses = weaknesses.DeepClone();
                    didRecoverFromLeap = true;
                } 

                // Cooldown
                if (!didLandFromLeap)
                {
                    IsRunning = false;

                    Controller.Rumble(0.25f, 0.35f);

                    Factory.SfxPlayer(name: "Bump", position: Transform.Position, parent: Entity, volume: 0.65f);
                    animationState = new AnimationState("LeapCooldown", 21, 22, false);
                    leapCooldownTime = leapCooldownDuration;

                    Factory.ParticleEffect(
                        scene: Entity.Scene,
                        position: Transform.Position + new Vector2(-4, 4),
                        amount: Chance.Range(1, 3),
                        size: new Vector2(4, 4),
                        speed: new Range(10f, 20f),
                        vectorDirection: new Vector2(-1, Chance.Range(0f, 0.5f)),
                        duration: 0.3f,
                        spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                        directionType: ParticleDirection.Vector,
                        animationState: new AnimationState("Idle", 1, 3, false, 10),
                        fadeOverTime: false,
                        additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
                    );
                    Factory.ParticleEffect(
                       scene: Entity.Scene,
                       position: Transform.Position + new Vector2(4, 4),
                       amount: Chance.Range(1, 3),
                       size: new Vector2(4, 4),
                       speed: new Range(10f, 20f),
                       vectorDirection: new Vector2(1, Chance.Range(-0, 0.5f)),
                       duration: 0.3f,
                       spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                       directionType: ParticleDirection.Vector,
                       animationState: new AnimationState("Idle", 1, 3, false, 10),
                       fadeOverTime: false,
                       additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
                   );
                }

                float acceleration = 0.0000001f;

                if (Collider.Info.DownTags.Contains("Ice"))
                {
                    acceleration = iceAcceleration;
                }

                // Normal
                if (didLandFromLeap && leapCooldownTime <= 0)
                {
                    // if (!didRecoverFromLeap)
                    // {
                    //     HurtHandler.Weaknesses = weaknesses.DeepClone();
                    //     didRecoverFromLeap = true;
                    // } 

                    int additionalOffset = 0;

                    if (GravityChanger.Direction.Y == 1)
                    {
                        if (input.Y < 0) additionalOffset = rowLength * 2;
                        else if (input.Y > 0) additionalOffset = rowLength * 4;
                    }
                    else if (GravityChanger.Direction.Y == -1)
                    { 
                        if (input.Y < 0) additionalOffset = rowLength * 4;
                        else if (input.Y > 0) additionalOffset = rowLength * 2;
                    }
                    else if (GravityChanger.Direction.X == 1)
                    {
                        if (input.X < 0) additionalOffset = rowLength * 2;
                        else if (input.X > 0) additionalOffset = rowLength * 4;
                    }
                    else if (GravityChanger.Direction.X == -1)
                    { 
                        if (input.X < 0) additionalOffset = rowLength * 4;
                        else if (input.X > 0) additionalOffset = rowLength * 2;
                    }

                    if (IsRunning)
                    {
                        additionalOffset += rowLength * 7;
                    }

                    if (!canShoot)
                    {
                        Animator.Offset = rowLength;
                    }
                    else
                    {
                        Animator.Offset = 0;
                    }

                    Animator.Offset += additionalOffset;

                    if (isBarking)
                    {
                        Animator.Offset = rowLength * 6;
                    }

                    animationState = new AnimationState("LeapLand" + additionalOffset, 18, 19, false);

                    if (IsRunning && input.X != 0) Sprite.FlipHorizontally = input.X == -1;
                    int dir = Sprite.FlipHorizontally ? -1 : 1;
                    float crawlSpeed = 30;

                    if (IsRunning) input.X = dir;

                    float desiredVelocity = IsRunning
                        ? dir * runSpeed
                        : input.H() * crawlSpeed;

                    if (input.H() != 0)
                    {
                        animationState = new AnimationState("LeapLandMove", 19, 20, true, 7f, new List<AnimationEvent>()
                        {
                            new AnimationEvent(20, _ =>
                            {
                                Factory.SfxPlayer(name: "Bump", position: Transform.Position, parent: Entity, volume: 0.25f, pitch: 1f);
                            })
                        });
                    }

                    if (Math.Floor(Animator.Frame) == 19) desiredVelocity = 0;

                    Mover.Velocity.H(Mover.Velocity.H().MoveOverTime(desiredVelocity, acceleration));
                }
                else
                {
                    if (GravityChanger.IsVertical) Mover.Velocity.X = Mover.Velocity.X.MoveOverTime(0, acceleration);
                    else if (GravityChanger.IsHorizontal) Mover.Velocity.Y = Mover.Velocity.Y.MoveOverTime(0, acceleration);
                }

                // Queue jump if pressing during cooldown
                if (didLandFromLeap && leapCooldownTime > 0 && Controller.Jump.IsPressed)
                {
                    queueJumpFromLeap = true;
                }

                if (GravityChanger.Direction.Y == 1) Mover.Velocity.Y = 0.1f;
                else if (GravityChanger.Direction.Y == -1) Mover.Velocity.Y = -0.1f;
                else if (GravityChanger.Direction.X == 1) Mover.Velocity.X = 0.1f;
                else if (GravityChanger.Direction.X == -1) Mover.Velocity.X = -0.1f;

                didLandFromLeap = true;

                if (leapCooldownTime <= 0)
                {
                    HandleSpit();
                    HandleBark();
                }

                // Set to Normal state
                if (IsFeetColliding && (Controller.Jump.IsPressed || queueJumpFromLeap) && leapCooldownTime <= 0)
                {
                    StatListener.IncrementJumps();

                    float pitch = Chance.Range(0f, 0.1f);

                    if (Controller.Jump.IsHeld)
                    {
                        Mover.Velocity.V(jumpSpeed * 0.65f);
                    }
                    else if (queueJumpFromLeap)
                    {
                        Mover.Velocity.V(minJumpSpeed);
                    }

                    Factory.SfxPlayer(name: "Jump", position: Transform.Position, pitch: pitch, parent: Entity, onlyOne: false);
                    Factory.ParticleEffect(
                        scene: Entity.Scene,
                        position: Transform.Position + new Vector2(-4, 4),
                        amount: Chance.Range(1, 3),
                        size: new Vector2(4, 4),
                        speed: new Range(10f, 20f),
                        vectorDirection: new Vector2(-1, Chance.Range(-0.25f, 0.25f)),
                        duration: 0.3f,
                        spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                        directionType: ParticleDirection.Vector,
                        animationState: new AnimationState("Idle", 1, 3, false, 10),
                        fadeOverTime: false,
                        additionalVelocity: new Vector2(Mover.Velocity.X * 0.5f, 0)
                    );
                    Factory.ParticleEffect(
                       scene: Entity.Scene,
                       position: Transform.Position + new Vector2(4, 4),
                       amount: Chance.Range(1, 3),
                       size: new Vector2(4, 4),
                       speed: new Range(10f, 20f),
                       vectorDirection: new Vector2(1, Chance.Range(-0.25f, 0.25f)),
                       duration: 0.3f,
                       spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                       directionType: ParticleDirection.Vector,
                       animationState: new AnimationState("Idle", 1, 3, false, 10),
                       fadeOverTime: false,
                       additionalVelocity: new Vector2(Mover.Velocity.X * 0.5f, 0)
                   );
                    Factory.ParticleEffect(
                        scene: Entity.Scene,
                        position: Transform.Position + new Vector2(0, 4),
                        amount: Chance.Range(1, 4),
                        size: new Vector2(4, 4),
                        speed: new Range(50f, 60f),
                        vectorDirection: new Vector2(Chance.Range(-0.25f, 0.25f), -1),
                        duration: 0.3f,
                        spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                        directionType: ParticleDirection.Vector,
                        animationState: new AnimationState("Idle", 1, 3, false, 10),
                        fadeOverTime: false,
                        additionalVelocity: new Vector2(Mover.Velocity.X * 0.5f, 0)
                    );
                    didJump = true;
                    NormalStart();
                }
            }
        }

        void LeapExit(string state)
        {
            HurtHandler.Weaknesses = weaknesses.DeepClone();

            didLandFromLeap = false;
            didRecoverFromLeap = false;
            queueJumpFromLeap = false;

            lockRunEffects = false;
            disableRunEffects = false;
        }

        // Dash (Water Leap)

        void DashStart()
        {
            ChangeState("Dash");

            Animator.Offset = 0;

            Vector2 dashInput = new Vector2(0, 0);
            if (!QuickSwap.IsEnabled)
            {
                if (Controller.Left.IsHeld) dashInput.X -= 1;
                if (Controller.Right.IsHeld) dashInput.X += 1;
                if (Controller.Up.IsHeld) dashInput.Y -= 1;
                if (Controller.Down.IsHeld) dashInput.Y += 1;
            }
            if (dashInput == Vector2.Zero)
            {
                if (SwimDirection == Direction.Up) dashInput.Y = -1;
                if (SwimDirection == Direction.Down) dashInput.Y = 1;
                if (SwimDirection == Direction.Left) dashInput.X = -1;
                if (SwimDirection == Direction.Right) dashInput.X = 1;
            }
            dashDirection = dashInput.Normalized();

            Mover.Velocity = dashDirection * dashSpeed * (IsRunning ? 1.5f : 1f);
            if (IsRunning) lockRunEffects = true;
            else disableRunEffects = true;

            HurtHandler.TriggerInvincibility(dashDuration * 0.5f);

            didDashShake = false;

            animationState = new AnimationState("Dash", 7);
            Factory.SfxPlayer(name: "Dash", position: Transform.Position, parent: Entity, pitch: -0.5f);
            Controller.Rumble(0.25f, 0.125f);

            dashTime = dashDuration;
        }

        void Dash()
        {
            dashTime -= Time.ModifiedDelta;

            if (Collider.Info.Up)
            {
                Mover.Velocity.Y *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }
            if (Collider.Info.Down)
            {
                Mover.Velocity.Y *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }
            if (Collider.Info.Left)
            {
                Mover.Velocity.X *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }
            if (Collider.Info.Right)
            {
                Mover.Velocity.X *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }

            int rotation = 1;
            if (Sprite.FlipVertically) rotation = -1;

            if (HurtHandler.IsInvincible)
            {
                Mover.Velocity = Mover.Velocity.MoveOverTime(Vector2.Zero, 0.25f);
                Sprite.Rotation += 7f * rotation * Time.Delta;
            }
            else
            {
                Mover.Velocity = Mover.Velocity.MoveOverTime(Vector2.Zero, 0.01f);
                Sprite.Rotation += 2f * rotation * Time.Delta;

                if (!didDashShake)
                {

                    Factory.SfxPlayer(name: "Swing", position: Transform.Position, pitch: Chance.Range(-0.75f, -0.5f));
                    Shaker.ShakeX();
                    animationState = new AnimationState("DashRecover", 23);
                    didDashShake = true;
                }
            }

            if (dashTime <= 0)
            {
                SwimStart();
            }
            else if (!IsTouchingWater)
            {
                JumpFromWater(dashDirection);
                ExitFromWater();
            }
        }

        void DashExit(string state)
        {
            if (dashDirection.X.Abs() > dashDirection.Abs().Y) Shaker.ShakeY();
            else Shaker.ShakeX();

            // if (state == "Normal") 
            // {
            //     ExitFromWater();
            // }

            lockRunEffects = false;
            disableRunEffects = false;
        }

        // Bounce

        void BounceStart()
        {
            ChangeState("Bounce");
            Controller.Rumble(0.25f, 0.125f);

            Factory.ParticleEffect(
                scene: Entity.Scene,
                position: Transform.Position + new Vector2(-4, 4),
                amount: Chance.Range(1, 3),
                size: new Vector2(4, 4),
                speed: new Range(10f, 20f),
                vectorDirection: new Vector2(-1, Chance.Range(0f, 0.5f)),
                duration: 0.3f,
                spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
                directionType: ParticleDirection.Vector,
                animationState: new AnimationState("Idle", 1, 3, false, 10),
                fadeOverTime: false,
                additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
            );

            Factory.ParticleEffect(
               scene: Entity.Scene,
               position: Transform.Position + new Vector2(4, 4),
               amount: Chance.Range(1, 3),
               size: new Vector2(4, 4),
               speed: new Range(10f, 20f),
               vectorDirection: new Vector2(1, Chance.Range(-0, 0.5f)),
               duration: 0.3f,
               spriteTemplate: new Sprite("PulsingCircle", 8, 1, Paint.White),
               directionType: ParticleDirection.Vector,
               animationState: new AnimationState("Idle", 1, 3, false, 10),
               fadeOverTime: false,
               additionalVelocity: new Vector2(Mover.Velocity.X * 0.25f, 0)
           );

            Factory.SfxPlayer(name: "Dash", position: Transform.Position, parent: Entity, pitch: 0.5f);
            animationState = new AnimationState("Bounce", 48);
            Animator.Offset = 0;

            weaknesses = HurtHandler.Weaknesses.DeepClone();

            if (GravityChanger.Direction.Y == 1)
            {
                if (Controller.Down.IsHeld && !IsFeetColliding)
                {
                    Mover.Velocity.Y = maxFallSpeed;
                    bounceRotationSpeed = 30f;
                    HurtHandler.Weaknesses = new List<string>() { "Pain" };
                }
                else
                {
                    if (Mover.Velocity.Y > bounceDoubleJumpSpeed) Mover.Velocity.Y = bounceDoubleJumpSpeed;
                    bounceRotationSpeed = 2.5f;
                }
            }
            else if (GravityChanger.Direction.Y == -1)
            {
                if (Controller.Up.IsHeld && !IsFeetColliding)
                {
                    Mover.Velocity.Y = -maxFallSpeed;
                    bounceRotationSpeed = 30f;
                    HurtHandler.Weaknesses = new List<string>() { "Pain" };
                }
                else
                {
                    if (Mover.Velocity.Y < -bounceDoubleJumpSpeed) Mover.Velocity.Y = -bounceDoubleJumpSpeed;
                    bounceRotationSpeed = 2.5f;
                }
            }
            else if (GravityChanger.Direction.X == 1)
            {
                if (Controller.Right.IsHeld && !IsFeetColliding)
                {
                    Mover.Velocity.X = maxFallSpeed;
                    bounceRotationSpeed = 30f;
                    HurtHandler.Weaknesses = new List<string>() { "Pain" };
                }
                else
                {
                    if (Mover.Velocity.X > bounceDoubleJumpSpeed) Mover.Velocity.X = bounceDoubleJumpSpeed;
                    bounceRotationSpeed = 2.5f;
                }
            }
            else if (GravityChanger.Direction.X == -1)
            {
                if (Controller.Left.IsHeld && !IsFeetColliding)
                {
                    Mover.Velocity.X = -maxFallSpeed;
                    bounceRotationSpeed = 30f;
                    HurtHandler.Weaknesses = new List<string>() { "Pain" };
                }
                else
                {
                    if (Mover.Velocity.X < -bounceDoubleJumpSpeed) Mover.Velocity.X = -bounceDoubleJumpSpeed;
                    bounceRotationSpeed = 2.5f;
                }
            }

            canEnterBounce = false;
            canBounceFastFall = true;
            queueBounceFastFall = false;
            DidJumpFromSpring = false;
            bounceHitTime = 0;
        }

        void Bounce()
        {
            bounceHitTime -= Time.ModifiedDelta;

            if (IsTouchingWater)
            {
                SwimStart();
                return;
            }

            if (BadgeIsPressed(Badge.Leap))
            {
                if (Controller.Left.IsHeld && !Controller.Right.IsHeld && !QuickSwap.IsEnabled && !BadgeIsHeld(Badge.QuickSwap)) Sprite.FlipHorizontally = true;
                else if (Controller.Right.IsHeld && !Controller.Left.IsHeld && !QuickSwap.IsEnabled && !BadgeIsHeld(Badge.QuickSwap)) Sprite.FlipHorizontally = false;
                LeapStart();
                return;
            }

            // if (BadgeIsPressed(Badge.Float))
            // {
            //     Flutter();
            //     return;
            // }

            if (DidJumpFromSpring)
            {
                ChangeState("Normal");
                return;
            }

            float acceleration = bounceMoveAcceleration;
            // if (!canBounceFastFall) acceleration *= 40f;
            if (GravityChanger.IsVertical)
            {
                float desiredVelocity = input.X * (IsRunning ? runSpeed : moveSpeed);
                if (!(Mover.Velocity.X.Abs() > desiredVelocity.Abs() && input.X == Math.Sign(Mover.Velocity.X)) && input.X != 0)
                {
                    Mover.Velocity.X = Mover.Velocity.X.MoveOverTime(desiredVelocity, acceleration);
                }
                if (GravityChanger.Direction.Y == 1)
                {
                    Mover.Velocity.Y += (gravity * (Mover.Velocity.Y >= 0f ? 2f : 1f)) * Time.ModifiedDelta;

                    if (Collider.Info.Down)
                    {
                        ChangeState("Normal");
                        Controller.Rumble(0.2f, 0.2f);
                        return;
                    }

                    if (Collider.Info.Up)
                    {
                        Factory.StarParticleEffect(position: Transform.Position);
                        Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: -0.5f);
                        Mover.Velocity.Y = 0;
                        bounceInvincibleCooldown = 0.225f;
                        canBounceFastFall = false;

                        return;
                    }
                }
                else if (GravityChanger.Direction.Y == -1)
                {
                    Mover.Velocity.Y += (-gravity * (Mover.Velocity.Y <= 0f ? 2f : 1f)) * Time.ModifiedDelta;

                    if (Collider.Info.Up)
                    {
                        ChangeState("Normal");
                        Controller.Rumble(0.2f, 0.2f);
                        return;
                    }

                    if (Collider.Info.Down)
                    {
                        Factory.StarParticleEffect(position: Transform.Position);
                        Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: -0.5f);
                        Mover.Velocity.Y = 0;
                        bounceInvincibleCooldown = 0.225f;
                        canBounceFastFall = false;

                        return;
                    }
                } 
            }
            else
            {
                float desiredVelocity = input.Y * (IsRunning ? runSpeed : moveSpeed);
                if (!(Mover.Velocity.Y.Abs() > desiredVelocity.Abs() && input.Y == Math.Sign(Mover.Velocity.Y)) && input.Y != 0)
                {
                    Mover.Velocity.Y = Mover.Velocity.Y.MoveOverTime(desiredVelocity, acceleration);
                }
                if (GravityChanger.Direction.X == 1)
                {
                    Mover.Velocity.X += (gravity * (Mover.Velocity.X >= 0f ? 2f : 1f)) * Time.ModifiedDelta;

                    if (Collider.Info.Right)
                    {
                        ChangeState("Normal");
                        Controller.Rumble(0.2f, 0.2f);
                        return;
                    }

                    if (Collider.Info.Left)
                    {
                        Factory.StarParticleEffect(position: Transform.Position);
                        Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: -0.5f);
                        Mover.Velocity.X = 0;
                        bounceInvincibleCooldown = 0.225f;
                        canBounceFastFall = false;

                        return;
                    }
                }
                else if (GravityChanger.Direction.X == -1)
                {
                    Mover.Velocity.X += (-gravity * (Mover.Velocity.X <= 0f ? 2f : 1f)) * Time.ModifiedDelta;

                    if (Collider.Info.Left)
                    {
                        ChangeState("Normal");
                        Controller.Rumble(0.2f, 0.2f);
                        return;
                    }

                    if (Collider.Info.Right)
                    {
                        Factory.StarParticleEffect(position: Transform.Position);
                        Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: -0.5f);
                        Mover.Velocity.X = 0;
                        bounceInvincibleCooldown = 0.225f;
                        canBounceFastFall = false;

                        return;
                    }
                } 
            }

            // if (Collider.Info.Left || Collider.Info.Right)
            // {
            //     Mover.Velocity.X *= -1f;
            // }

            bool doFastFall = false;
            if (GravityChanger.Direction.Y == 1 && Controller.Down.IsPressed) doFastFall = true;
            else if (GravityChanger.Direction.Y == -1 && Controller.Up.IsPressed) doFastFall = true;
            else if (GravityChanger.Direction.X == 1 && Controller.Right.IsPressed) doFastFall = true;
            else if (GravityChanger.Direction.X == -1 && Controller.Left.IsPressed) doFastFall = true;

            if (!QuickSwap.IsEnabled && (doFastFall || queueBounceFastFall))
            {
                if (canBounceFastFall)
                {
                    Mover.Velocity.V(maxFallSpeed);
                    queueBounceFastFall = false;
                }
                else
                {
                    queueBounceFastFall = true;
                }
            }

            bounceInvincibleCooldown -= Time.ModifiedDelta;

            bool invincible = false;
            float invincibleThreshold = 10f;
            if (bounceInvincibleCooldown <= 0)
            {
                Sprite.Position = Vector2.Zero;
                if (Mover.Velocity.Y >= -invincibleThreshold && GravityChanger.Direction.Y == 1) invincible = true;
                if (Mover.Velocity.Y <= invincibleThreshold && GravityChanger.Direction.Y == -1) invincible = true;
                if (Mover.Velocity.X >= -invincibleThreshold && GravityChanger.Direction.X == 1) invincible = true;
                if (Mover.Velocity.X <= invincibleThreshold && GravityChanger.Direction.X == -1) invincible = true;
            }
            else 
            {
                Sprite.Position = Utility.Shake(2);
            }

            if (bounceHitTime <= 0) canBounceFastFall = true;

            // TintOverride = null;
            if (canBounceFastFall)
            {
                if (invincible)
                {
                    if (animationState.Name != "BounceStrong")
                    {
                        // if (GravityChanger.IsVertical) Shaker.ShakeX();
                        // else Shaker.ShakeY();
                        // Factory.SfxPlayer(name: "Swing", pitch: -1f, volume: 0.5f);
                        // Factory.TinkParticleEffect(position: Transform.Position, color: Paint.White);
                    }
                    animationState = new AnimationState("BounceStrong", 48);
                    // animationState = new AnimationState("BounceStrong", 47);
                    // TintOverride = Paint.Rainbow.Random();
                    lockRunEffects = true;
                }
                else
                {
                    animationState = new AnimationState("Bounce", 48);
                }
            }
            else
            {
                animationState = new AnimationState("BounceWeak", 48);
                // animationState = new AnimationState("BounceWeak", 23);
                lockRunEffects = false;
            }

            if (bounceInvincibleCooldown > 0)
            {
                animationState = new AnimationState("BounceBonked", 23);
            }

            bounceRotationSpeed = bounceRotationSpeed.MoveTowardsWithSpeed(invincible ? 50f : 2f, 100f);
            // if (invincible) TintOverride = Paint.White * 0.5f;
            // else TintOverride = null;
            Sprite.Rotation += (Sprite.FlipHorizontally ? -bounceRotationSpeed : bounceRotationSpeed) * Time.ModifiedDelta;

            // Invincible
            if (invincible)
            {
                canBounceFastFall = true;

                if (HurtHandler.Weaknesses.Count > 0)
                {
                    HurtHandler.Weaknesses = new List<string>() { "Pain" };
                }

                List<Transform> hits = Collider.GetCollisions(new List<string>() { "BulletHurt", "PlayerHurt", "Npc", "Bounceable" })
                    .Where(t =>
                    {
                        Liver tL = t.Entity.GetComponent<Liver>();
                        return !t.Entity.Tags.Contains("NotBounceable") && (tL == null || tL.Active);
                    })
                    .ToList();

                if (hits.Count > 0)
                {
                    Controller.Rumble(0.25f, 0.25f);

                    Factory.SfxPlayer(name: "Dash", position: Transform.Position, parent: Entity, pitch: -0.5f);
                    if (GravityChanger.Direction.Y == 1) Mover.Velocity.Y = bounceJumpSpeed * (input.Y == 1 ? 0.775f : 1f);
                    else if (GravityChanger.Direction.Y == -1) Mover.Velocity.Y = -bounceJumpSpeed;
                    else if (GravityChanger.Direction.X == 1) Mover.Velocity.X = bounceJumpSpeed;
                    else if (GravityChanger.Direction.X == -1) Mover.Velocity.X = -bounceJumpSpeed;
                    canBounceFastFall = false;
                     HurtHandler.Weaknesses = new List<string>() { };
                    bounceHitTime = 0.25f;

                    foreach (Transform hit in hits)
                    {
                        // Mover.Velocity.X += (Transform.Position.X - hit.Position.X) * 16;
                        // if (Mover.Velocity.X.Abs() > runSpeed)
                        // {
                        //     Mover.Velocity.X = runSpeed * MathF.Sign(Mover.Velocity.X);
                        // }

                        // Hurter hitHurter = hit.Entity.GetComponent<Hurter>();
                        // if (hitHurter?.IsAvailable == true)
                        // {
                        //     if (hitHurter.LandedHit != null) hitHurter.LandedHit(hitHurter, Entity);
                        // }

                        Activateable activateable = hit.Entity.GetComponent<Activateable>();
                        if (activateable?.IsAvailable == true)
                        {
                            activateable.Activate(Transform);
                        }

                        Liver hitLiver = hit.Entity.GetComponent<Liver>();
                        if (hitLiver?.IsAvailable == true)
                        {
                            HurtHandler hitHurtHandler = hit.Entity.GetComponent<HurtHandler>();
                            if (hitHurtHandler?.IsAvailable == true && hitHurtHandler.Indestructible) hitHurtHandler.Deflect(Mover);
                            else hitLiver.LoseHealth(Hurter);
                        }

                        Shaker hitShaker = hit.Entity.GetComponent<Shaker>();
                        if (hitShaker?.IsAvailable == true)
                        {
                            hitShaker.ShakeY();
                        }

                        PlayerGhost hitPlayerGhost = hit.Entity.GetComponent<PlayerGhost>();
                        if (hitPlayerGhost?.IsAvailable == true)
                        {
                            hitPlayerGhost.Bounced();
                        }

                        ColoredButton coloredButton = hit.Entity.GetComponent<ColoredButton>();
                        if (coloredButton?.IsAvailable == true)
                        {
                            coloredButton.Activate();
                        }

                        ClockBlock clockBlock = hit.Entity.GetComponent<ClockBlock>();
                        if (clockBlock?.IsAvailable == true)
                        {
                            clockBlock.Hit();
                        }

                        LandHit(hit.Entity);
                    }
                }
            }
            // Give back weaknesses
            else if (canBounceFastFall)
            {
                HurtHandler.Weaknesses = weaknesses.DeepClone();
            }
        }

        void BounceExit(string state)
        {
            HurtHandler.Weaknesses = weaknesses.DeepClone();

            Sprite.Rotation = GravityChanger.Rotation;
            TintOverride = null;

            canEnterBounce = true;
            lockRunEffects = false;
        }

        // PingPong (Water Bounce)

        void PingPongStart()
        {
            ChangeState("PingPong");

            Animator.Offset = 0;

            Vector2 pingPongInput = new Vector2(0, 0);
            if (!QuickSwap.IsEnabled)
            {
                if (Controller.Left.IsHeld) pingPongInput.X -= 1;
                if (Controller.Right.IsHeld) pingPongInput.X += 1;
                if (Controller.Up.IsHeld) pingPongInput.Y -= 1;
                if (Controller.Down.IsHeld) pingPongInput.Y += 1;
            }
            if (pingPongInput == Vector2.Zero)
            {
                if (SwimDirection == Direction.Up) pingPongInput.Y = -1;
                if (SwimDirection == Direction.Down) pingPongInput.Y = 1;
                if (SwimDirection == Direction.Left) pingPongInput.X = -1;
                if (SwimDirection == Direction.Right) pingPongInput.X = 1;
            }
            pingPongDirection = pingPongInput.Normalized();

            Mover.Velocity = pingPongDirection * pingPongSpeed * (IsRunning ? 1.5f : 1f);
            // if (IsRunning) lockRunEffects = true;
            // else disableRunEffects = true;

            didPingPongShake = false;

            animationState = new AnimationState("PingPong", 48);
            Factory.SfxPlayer(name: "Dash", position: Transform.Position, parent: Entity, pitch: -0.25f);
            Controller.Rumble(0.25f, 0.125f);

            pingPongTime = pingPongDuration;

            HurtHandler.Weaknesses = new List<string>() { "Pain" };
        }

        void PingPong()
        {
            pingPongTime -= Time.ModifiedDelta;

            if (Collider.Info.Up)
            {
                Mover.Velocity.Y *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }
            if (Collider.Info.Down)
            {
                Mover.Velocity.Y *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }
            if (Collider.Info.Left)
            {
                Mover.Velocity.X *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }
            if (Collider.Info.Right)
            {
                Mover.Velocity.X *= -1;
                Factory.SfxPlayer(name: "Bump", position: Transform.Position, pitch: Chance.Range(-0.5f, 0f));
                IsRunning = false;
            }

            float rotation = 2f;
            if (Sprite.FlipVertically) rotation = -2;

            float activeDuration = pingPongDuration * 0.75f;

            if (pingPongTime >= activeDuration)
            {
                lockRunEffects = true;
                float rotationFactor = (pingPongTime - activeDuration) / (pingPongDuration - activeDuration);
                if (rotationFactor < 0.25f) rotationFactor = 0.25f;
                Mover.Velocity = Mover.Velocity.MoveOverTime(Vector2.Zero, 0.25f);
                Sprite.Rotation += 30f * rotationFactor * rotation * Time.Delta;

                List<Transform> hits = Collider.GetCollisions(new List<string>() { "ContactHurt", "BulletHurt", "Decoration", "PlayerHurt", "Npc", "Bounceable" })
                    .Where(t =>
                    {
                        Liver tL = t.Entity.GetComponent<Liver>();
                        return !t.Entity.Tags.Contains("NotBounceable")
                            && (tL == null || tL.Active)
                            && (lastPingPongHitId == null || t.Entity.Id != lastPingPongHitId);
                    })
                    .ToList();

                if (hits.Count > 0)
                {
                    Controller.Rumble(0.25f, 0.25f);

                    Factory.SfxPlayer(name: "Dash", position: Transform.Position, parent: Entity, pitch: -0.75f);
                    canBounceFastFall = false;

                    Vector2 direction = (Transform.Position - hits[0].Position).Normalized();
                    lastPingPongHitId = hits[0].Entity.Id;

                    if (input != Vector2.Zero) direction = input;
                    // direction += input * 2f;
                    direction.Normalize();

                    Mover.Velocity = direction * pingPongSpeed * (IsRunning ? 1.5f : 1f);

                    pingPongTime = pingPongDuration;

                    if (Mover.Velocity.X.Abs() > Mover.Velocity.Y.Abs()) Shaker.ShakeY();
                    else Shaker.ShakeX();

                    foreach (Transform hit in hits)
                    {
                        Liver hitLiver = hit.Entity.GetComponent<Liver>();
                        if (hitLiver?.IsAvailable == true)
                        {
                            HurtHandler hitHurtHandler = hit.Entity.GetComponent<HurtHandler>();
                            if (hitHurtHandler?.IsAvailable == true && hitHurtHandler.Indestructible) hitHurtHandler.Deflect(Mover);
                            else hitLiver.LoseHealth(Hurter);
                        }

                        Shaker hitShaker = hit.Entity.GetComponent<Shaker>();
                        if (hitShaker?.IsAvailable == true)
                        {
                            hitShaker.ShakeY();
                        }

                        PlayerGhost hitPlayerGhost = hit.Entity.GetComponent<PlayerGhost>();
                        if (hitPlayerGhost?.IsAvailable == true)
                        {
                            hitPlayerGhost.Bounced();
                        }

                        Activateable Activateable = hit.Entity.GetComponent<Activateable>();
                        if (Activateable?.IsAvailable == true)
                        {
                            Activateable.Activate(Transform);
                        }

                        ColoredButton coloredButton = hit.Entity.GetComponent<ColoredButton>();
                        if (coloredButton?.IsAvailable == true)
                        {
                            coloredButton.Activate();
                        }
                    }
                }
            }
            else
            {
                lockRunEffects = false;
                Mover.Velocity = Mover.Velocity.MoveOverTime(Vector2.Zero, 0.01f);
                Sprite.Rotation += 1f * rotation * Time.Delta;
                HurtHandler.Weaknesses = weaknesses;

                if (!didPingPongShake)
                {
                    Shaker.ShakeX();
                    Factory.SfxPlayer(name: "Swing", position: Transform.Position, pitch: Chance.Range(-0.75f, -0.5f));
                    animationState = new AnimationState("PingPongRecover", 23);
                    didPingPongShake = true;
                }
            }

            if (pingPongTime <= 0)
            {
                SwimStart();
            }
            else if (!IsTouchingWater)
            {
                HurtHandler.Weaknesses = weaknesses;

                JumpFromWater(pingPongDirection);
                ExitFromWater();
                if (didForceJump) BounceStart();
            }
            else if (BadgeIsPressed(Badge.Leap))
            {
                DashStart();
            }
        }

        void PingPongExit(string state)
        {
            if (pingPongDirection.X.Abs() > pingPongDirection.Abs().Y) Shaker.ShakeY();
            else Shaker.ShakeX();

            lastPingPongHitId = null;
            lockRunEffects = false;
            disableRunEffects = false;

            // if (state == "Normal")
            // {
            //     ExitFromWater();
            // }

            HurtHandler.Weaknesses = weaknesses;
        }

        // Talk

        public void TalkStart(Transform target, int targetDistance = 20)
        {
            isBarking = false;
            IsRunning = false;

            talkReturnState = State;
            ChangeState("Talk");

            Mover.Forces.Clear();
            // if (Pusher?.IsAvailable == true) Pusher.Active = false;

            if (weaknesses.Count > 0) HurtHandler.Weaknesses = weaknesses.DeepClone();

            this.target = target;
            this.targetOffset = targetDistance;

            Animator.Offset = 0;

            targetGoOffLedges = false;
            ReachedDestination = false;
        }

        void Talk()
        {
            if (target == null && OtherPlayer?.IsAvailable == true) target = OtherPlayer.target;
            if (IsTouchingWater)
            {
                Mover.Velocity = Vector2.Zero;
                animationState = animationSwim;
            }
            else
            {
                Mover.Velocity.Y += gravity * Time.ModifiedDelta;
                if (IsFeetColliding) Mover.Velocity.Y = 0.1f;

                if (target?.IsAvailable == true)
                {
                    Sprite.FlipHorizontally = target.Position.X < Transform.Position.X;

                    bool canGo = true;

                    if (!targetGoOffLedges)
                    {
                        if (Mover.Velocity.X <= 0 && !Collider.IsTouching(Transform.BottomLeft + new Vector2(4, 2))) canGo = false;
                        if (Mover.Velocity.X >= 0 && !Collider.IsTouching(Transform.BottomRight + new Vector2(-4, 2))) canGo = false;
                    }

                    if (ReachedDestination || !canGo)
                    {
                        if (IsTouchingWater) animationState = animationSwim;
                        else animationState = new AnimationState("Idle", 8, 9, false, 8);
                        
                        // if (target.Position.X < Transform.Position.X)
                        // {
                        //     Transform.Position.X = target.Position.X + targetOffset;
                        // }
                        // else
                        // {
                        //     Transform.Position.X = target.Position.X + -targetOffset;
                        // }

                        Mover.Velocity.X = 0;
                    }
                    else
                    {
                        ReachedDestination = Math.Abs(target.Position.X - Transform.Position.X) >= targetOffset;

                        if (IsTouchingWater) animationState = animationSwimGo;
                        else animationState = new AnimationState("Walk", 2, 5, true, 8);

                        if (target.Position.X < Transform.Position.X)
                        {
                            Mover.Velocity.X = 40;
                        }
                        else
                        {
                            Mover.Velocity.X = -40;
                        }

                        if (Collider.Info.Left || Collider.Info.Right) animationState = new AnimationState("Idle", 8, 9, false, 9);
                    }
                }
            }

            if (!MyGame.IsTalking)
            {
                if (talkReturnState == "Swim")
                {
                    SwimStart();
                }
                else
                {
                    NormalStart();
                }
            }
        }

        public void TalkExit(string state)
        {

        }

        // Target

        public void TargetStart(Vector2 position, int offset = 2, bool? flip = null, bool absolute = false, float speed = 60, bool goOffLedges = true)
        {
            if (targetPosition == position) return;
            
            ChangeState("Target");

            isBarking = false;
            IsRunning = false;
            targetSpeed = speed;
            targetGoOffLedges = goOffLedges;

            if (weaknesses.Count > 0) HurtHandler.Weaknesses = weaknesses.DeepClone();

            targetPosition = position;
            this.targetOffset = offset;
            this.targetAbsolute = absolute;
            if (this.targetAbsolute)
            {
                Collider.DoesCollideWithGround = false;
                Mover.Velocity.Y = 0;
            }

            Sprite.FlipHorizontally = flip.HasValue ? flip.Value : position.X < Transform.Position.X;
            ReachedDestination = false;
            Animator.Offset = 0;
        }

        void Target()
        {
            if (!targetAbsolute && !IsTouchingWater)
            {
                Mover.Velocity.Y += gravity * Time.ModifiedDelta;
            }
            if (IsFeetColliding) Mover.Velocity.Y = 0.1f;

            ReachedDestination = true;
            bool away = false;

            if (Transform.Position.X < targetPosition.Value.X - targetOffset) ReachedDestination = false;
            if (Transform.Position.X > targetPosition.Value.X + targetOffset) ReachedDestination = false;
            if (Math.Abs(Transform.Position.X - targetPosition.Value.X) < targetOffset)
            {
                ReachedDestination = false;
                away = true;
            }
            if (Math.Abs((Math.Abs(Transform.Position.X - targetPosition.Value.X) - targetOffset)) < 2)
            {
                ReachedDestination = true;
            }

            bool canGo = true;

            if (!targetGoOffLedges)
            {
                if (Mover.Velocity.X < 0 && !Collider.IsTouching(Transform.BottomLeft + new Vector2(4, 2))) canGo = false;
                if (Mover.Velocity.X > 0 && !Collider.IsTouching(Transform.BottomRight + new Vector2(-4, 2))) canGo = false;
            }

            if (!ReachedDestination && canGo)
            {
                if (IsTouchingWater) animationState = animationSwimGo;
                else animationState = new AnimationState("Walk", 2, 5, true, 9);

                if (targetPosition.Value.X < Transform.Position.X)
                {
                    Mover.Velocity.X = -targetSpeed * (away ? -1 : 1);
                }
                else
                {
                    Mover.Velocity.X = targetSpeed * (away ? -1 : 1);
                }

                if (Collider.Info.Left || Collider.Info.Right) animationState = new AnimationState("Idle", 8, 9, false, 9);
            }
            else
            {
                if (IsTouchingWater) animationState = new AnimationState("Swim", 37, 40, true, 4);
                else animationState = new AnimationState("Idle", 8, 9, false, 9);

                if (targetPosition.Value.X < Transform.Position.X)
                {
                    Transform.Position.X = targetPosition.Value.X + targetOffset;
                }
                else
                {
                    Transform.Position.X = targetPosition.Value.X + -targetOffset;
                }

                Mover.Velocity.X = 0;
            }
        }

        public void TargetExit(string state)
        {
            targetPosition = null;

            if (this.targetAbsolute)
            {
                this.targetAbsolute = false;
                Collider.DoesCollideWithGround = true;
            }
        }

        // Stand

        public void StandStart(bool lookUp = false)
        {
            isBarking = false;
            IsRunning = false;

            if (IsStupid) UnscrambleControls();

            ChangeState("Stand");
            spitTime = 0;

            if (lookUp)
            {
                Animator.Offset = rowLength * 2;
            }
            else
            {
                Animator.Offset = 0;
            }
        }

        void Stand()
        {
            if (IsTouchingWater)
            {
                Mover.Velocity.X = 0;
                Mover.Velocity.Y = 0;
                animationState = new AnimationState("Swim", 37, 40, true, 4);
            }
            else
            {
                Mover.Velocity.V(Mover.Velocity.V() + (gravity * Time.ModifiedDelta));
                animationState = new AnimationState("Idle2", 8, 9, false);
            }
            if (IsFeetColliding)
            {
                Mover.Velocity.V(0.1f);
                Transform treadmill = Collider.Info.ActorCollisions.Find(c =>
                    c.Entity.Tags.Contains("Treadmill") && c.Top.Y >= Transform.Bottom.Y && c.Entity.GetComponent<Treadmill>()?.Enabled == true
                );
                if (treadmill == null)
                {
                    Mover.Velocity.H(0);
                }
            }
        }

        public void StandExit(string state)
        {

        }

        // Launch

        public void LaunchStart(Vector2 target)
        {
            ChangeState("Launch");

            Collider.DoesCollideWithGround = false;
            Mover.Velocity = Utility.BallTrajectory(Transform.Position, target);
            Animator.Offset = 0;
            animationState = new AnimationState("ContactHurt", 10);
            HurtHandler.Active = false;
        }

        void Launch()
        {
            fellRotation = fellRotation.MoveOverTime((MathF.Max(1f, MathF.Min(10f, Mover.Velocity.X.Abs()))) * (Mover.Velocity.X > 0 ? 1 : -1), 0.5f);
            Sprite.Rotation += fellRotation * Time.Delta;
            Mover.Velocity.Y += CONSTANTS.GRAVITY * Time.ModifiedDelta;
            if (Mover.Velocity.Y > 0)
            {
                Collider.DoesCollideWithGround = true;
                HurtHandler.Active = true;
            }
            if (Collider.Info.Down)
            {
                Sprite.Rotation = GravityChanger.Rotation;
                Mover.Velocity.X = Mover.Velocity.X.MoveOverTime(0f, 0.001f);
                Mover.Velocity.Y = 0.1f;
                animationState = new AnimationState("Idle", 8, 9, false);
            }
        }

        public void LaunchExit(string state)
        {
            HurtHandler.Active = true;
            Collider.DoesCollideWithGround = true;
            Sprite.Rotation = GravityChanger.Rotation;
        }

        // Win

        public void WinStart()
        {
            if (IsTouchingWater) return;
            ChangeState("Win");
            spitTime = 0;
        }

        void Win()
        {
            // Mover.Velocity.X = 0;
            Mover.Velocity.Y += gravity * Time.ModifiedDelta;
            if (IsFeetColliding) Mover.Velocity.Y = 0.1f;
            animationState = new AnimationState("Win", 31, 36, true, 6);
            Animator.Offset = 0;
        }

        public void WinExit(string state)
        {

        }

        #endregion

        #region  Utility

        public bool RecoverHealth()
        {
            int health = Liver.Health;
            if (breakBehavior?.IsAvailable == true)
            {
                if (breakBehavior.Charge != breakBehavior.ChargeMax) breakBehavior.Reset();
                breakBehavior.Charge++;
                if (breakBehavior.Charge > breakBehavior.ChargeMax) breakBehavior.Charge = breakBehavior.ChargeMax;
            }
            if (IsStupid)
            {
                UnscrambleControls();
                RecoilStart(duration: 0.9f);
            }
            Liver.GainHealth(1);
            UiHealthBar.Popup(recover: true);
            Shaker.ShakeY();
            Factory.HeartParticleEffect(position: Transform.Position);
            if (Liver.Health > health) return true;
            else return false;
        }

        public void ForceFellState()
        {
            if (IsTouchingWater) SwimStart();
            else NormalStart();

            fellOffScreen = true;
            fellOffScreenAndInAir = true;
            fellOffScreenCount = 1;
            fellRotation = 0;
            fellOffScreenCanRecover = false;
            IsFluttering = false;
            IsRunning = false;
        }

        /// <summary>Combine Main, NoTint, and Accessory Textures into one Texture.</summary>
        public static SafeRenderTarget2D BuildTexture(bool isPlayerOne = true, bool isIcon = false, bool isGhost = false, bool isGhostIcon = false, SaveFile file = null)
        {
            if (file == null) file = SaveData.Current;
            if (file == null)
            {
                file = new SaveFile();
            }

            bool isPlatformer = !isGhost && !isIcon && !isGhostIcon;

            string costume = isPlayerOne ? file.Costume : file.CostumePlayerTwo;
            string costumeBase = costume;
            if (isGhost) costume += "Ghost";
            if (isIcon) costume += "Icon";
            if (isGhostIcon) costume += "GhostIcon";
            string fallback = costume.Replace(costumeBase, "Chickpea");
            string accessory = isPlayerOne ? file.Accessory : file.AccessoryPlayerTwo;
            if (accessory.HasValue()) accessory = $"_{accessory}";
            PaintColor tint = PaintColor.PaintColors.Find(p => p.Name == (isPlayerOne ? file.Tint : file.TintPlayerTwo));

            // ex) Frankie NoEars
            string special = "";

            if (accessory?.HasValue() == true)
            {
                special = Lib.Textures.Where(t => t.Key.StartsWith($"{costume}{accessory}"))
                    .Select(t => t.Key)
                    .Select(n => n.RemoveSubString($"{costume}{accessory}")
                        .RemoveSubString("_NoTint")
                        .RemoveSubString("_Palettes")
                        .RemoveSubString("_Position")
                    )
                    .Where(n => n?.HasValue() == true)
                    .FirstOrDefault();
            }

            List<Texture2D> textures = new List<Texture2D>() { };
            Effect effect = null;

            int index = 0;
            if (tint != null)
            {
                index = tint.Index;
                // if (tint.Name == "Dynamic") index--;
            }

            // Main
            if (tint != null)
            {
                Texture2D paletteTexture = Lib.GetTexture($"{costumeBase}_Palettes");
                // if (paletteTexture == null) paletteTexture = Lib.GetTexture($"{fallback}Palettes");
                if (paletteTexture != null)
                {
                    Color[][] palettes = Shapes.ReadPaletteByRows(paletteTexture);
                    if (palettes.Count() > index) effect = Shapes.PrepTintEffect(palettes[0], palettes[index]);
                }
            }
            Texture2D textureMain = Lib.GetTexture($"{costume}{special}");
            if (textureMain == null) textureMain = Lib.GetTexture($"{fallback}");
            textures.Add(Shapes.ApplyEffectToTexture(textureMain, effect));

            Texture2D textureNoTint = Lib.GetTexture($"{costume}{special}_NoTint");
            // if (textureNoTint == null) textureNoTint = Lib.GetTexture($"{fallback}_NoTint");
            if (textureNoTint != null) textures.Add(textureNoTint);

            // Accessory
            if (accessory.HasValue())
            {
                if (tint != null)
                {
                    Texture2D accessoryPaletteTexture = Lib.GetTexture($"{costumeBase}{accessory}_Palettes");
                    // if (accessoryPaletteTexture == null) accessoryPaletteTexture = Lib.GetTexture($"{fallback}{accessory}Palettes");
                    if (accessoryPaletteTexture != null)
                    {
                        Color[][] palettes = Shapes.ReadPaletteByRows(accessoryPaletteTexture);
                        if (palettes.Count() > index) effect = Shapes.PrepTintEffect(palettes[0], palettes[index]);
                    }
                }

                // Mask
                // if (accessory == "_Mask" && isPlatformer && File.Exists(Paths.DrawingPath(SaveData.Current, "Mask")) && textures.First() != null)
                // {
                //     Dictionary<Color, List<Vector2>> allMaskPositions = Shapes.ReadPositionsOfColors(Lib.GetTexture($"{costumeBase}_Mask_Position"));
                //     List<(Color Color, Vector2 Position)> maskPositions = new List<(Color, Vector2)>() { };
                //     foreach (KeyValuePair<Color, List<Vector2>> entry in allMaskPositions)
                //     {
                //         foreach (Vector2 position in entry.Value)
                //         {
                //             maskPositions.Add((entry.Key, position));
                //         }
                //     }

                //     SafeRenderTarget2D maskTarget = new SafeRenderTarget2D(MyGame.Graphics.GraphicsDevice, textures.First().Width, textures.First().Height, "MaskTarget");
                //     Texture2D maskDrawing = Texture2D.FromFile(MyGame.Instance.GraphicsDevice, Paths.DrawingPath(SaveData.Current, "Mask" + (isPlayerOne ? "" : "2")));
                //     if (maskDrawing.Width != CONSTANTS.MASK_TEXTURE_SIZE || maskDrawing.Height != CONSTANTS.MASK_TEXTURE_SIZE) maskDrawing = Shapes.ResizePng(maskDrawing, CONSTANTS.MASK_TEXTURE_SIZE, CONSTANTS.MASK_TEXTURE_SIZE, "Mask");
                //     MyGame.Graphics.GraphicsDevice.SetRenderTarget(maskTarget);
                //     MyGame.Graphics.GraphicsDevice.Clear(Color.Transparent);
                //     MyGame.SpriteBatch.Begin(
                //         sortMode: SpriteSortMode.Deferred,
                //         samplerState: SamplerState.PointClamp,
                //         transformMatrix: Camera.ViewportTransformation,
                //         blendState: BlendState.AlphaBlend
                //     );

                //     int tileSize = 48;

                //     int i = 0;
                //     for (int y = 0; y < maskTarget.Height; y += tileSize)
                //     {
                //         for (int x = 0; x < maskTarget.Width; x += tileSize)
                //         {
                //             i++;
                //             (Color Color, Vector2 Position) entry = maskPositions.Find(p => p.Position.X > x && p.Position.X < x + tileSize && p.Position.Y > y && p.Position.Y < y + tileSize);
                //             Vector2 offset = new Vector2(entry.Position.X % tileSize, entry.Position.Y % tileSize);
                //             Shapes.DrawSprite(texture: maskDrawing, position: new Vector2(x, y) + offset + new Vector2(CONSTANTS.MASK_TEXTURE_SIZE / 2f, CONSTANTS.MASK_TEXTURE_SIZE / 2f), rotation: (entry.Color.R >= entry.Color.B && entry.Color.R >= entry.Color.G) ? 0f : (entry.Color.B >= entry.Color.R && entry.Color.B >= entry.Color.G) ? MathHelper.PiOver2 * 3f : MathHelper.PiOver2, origin: new Vector2(CONSTANTS.MASK_TEXTURE_SIZE / 2f));
                //         }
                //     }
                //     MyGame.SpriteBatch.End();
                //     MyGame.Graphics.GraphicsDevice.SetRenderTarget(MyGame.GameportRenderTarget);
                //     textures.Add(maskTarget);
                // }

                Texture2D textureMainAccessory = Lib.GetTexture($"{costume}{accessory}{special}");
                // if (textureMainAccessory == null) textureMainAccessory = Lib.GetTexture($"{fallback}{accessory}");
                if (textureMainAccessory != null) textures.Add(Shapes.ApplyEffectToTexture(textureMainAccessory, effect));
                
                Texture2D textureNoTintAccessory = Lib.GetTexture($"{costume}{accessory}{special}_NoTint");
                // if (textureNoTintAccessory == null) textureNoTintAccessory = Lib.GetTexture($"{fallback}{accessory}_NoTint");
                if (textureNoTintAccessory != null) textures.Add(textureNoTintAccessory);
            }

            SafeRenderTarget2D texture = Shapes.CombineTextures(textures);

            // texture.Name = $"{costume}{accessory}{special}";
            texture.Name = (isPlayerOne ? "PlayerOne" : "PlayerTwo");
            if (isIcon) texture.Name += "Icon";
            else if (isGhost) texture.Name += "Ghost";
            else if (isGhostIcon) texture.Name += "GhostIcon";

            return texture;
        }

        public void LandHit(Entity victim)
        {
            // Charge Break Badge with hits
            // HealthRecoverer vHealthRecoverer = victim.GetComponent<HealthRecoverer>();

            // List<string> okayTags = new List<string>() { "Enemy", "Projectile", "Boss" };
            // && okayTags.Any(t => victim.Tags.Contains(t))

            // vHealthRecoverer?.IsRecovering != true

            // if (true)
            // {
            //     breakTime -= 1;
            //     breakBehavior?.Shaker?.ShakeY();
            //     if (breakTime <= 0)
            //     {
            //         breakTime = 0;
            //         if (CanUseBreakBadge && breakBehavior?.CanReset == true)
            //         {
            //             breakBehavior.Reset();
            //         }
            //     }
            //     else
            //     {
            //         if (breakBehavior?.IsAvailable == true) Factory.DustParticleEffect(position: breakBehavior.Transform.Position, amount: 2, order: breakBehavior.Sprite.Order - 1);
            //         Factory.SfxPlayer(name: "SineShort", pitch: 1 - (breakTime / breakDuration), position: Transform.Position, volume: 0.25f);
            //     }
            // }
        }

        public void SurviveDamage(int factor, Hurter hurter, bool canShare = false)
        {
            if (SaveData.Current != null && SaveData.Current?.Recover != "Room")
            {
                if (IsPlayerOne) SaveData.Current.StoredHealth = Liver.Health;
                else SaveData.Current.StoredHealthPlayerTwo = Liver.Health;
            }

            bool wasStupid = false;
            if (hurter?.Entity?.Tags?.Contains("Spore") == true)
            {
                HurtTime = 0;
                HealTime = 1.5f;
                
                wasStupid = true;
                ScrambleControls();
            }
            else
            {
                HurtTime = 1.25f;
                HealTime = 0;

                if (IsStupid)
                {
                    wasStupid = true;
                    UnscrambleControls();
                }
            }
            Controller.Rumble(0.4f, 0.5f);
            StatListener.IncrementHurts();

            if (fellOffScreen && hurter != null)
            {
                ExitFallOffScreen();
                fellOffScreenAndInAir = false;
            }

            if (hurter != null && hurter.Damage == 0) HurtHandler.ResetInvincibility();

            else if (hurter != null && hurter.Entity.Tags.Contains("Spiderweb"))
            {
                webbedTime = webbedDuration;
                if (State == "Swim") SwimStart();
                else NormalStart();
            }
            else
            {
                RecoilStart(hurter?.Entity.GetComponent<Transform>(), wasStupid ? 0.9f : recoilDuration);
                webbedTime = -1;
            }

            if (canShare && SaveData.Current?.SharedHitPoints == true && MyGame.PlayerCount == 2 && OtherPlayer?.IsAvailable == true)
            {
                if (Entity.Tags.Contains("PlayerTwo"))
                {
                    RopeTime = ropeDuration;
                    OtherPlayer.ropeColor = Paint.Pink;
                }
                else
                {
                    RopeTime = ropeDuration;
                    ropeColor = Paint.Pink;
                }
                OtherPlayer.Liver.Health = Liver.Health;
                OtherPlayer.SurviveDamage(factor, hurter, false);
            }
        }

        #endregion
    }
}
