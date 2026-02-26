using BepInEx;
using Expedition;
using ImprovedInput;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;
using System.Xml.Linq;
using UnityEngine;
using Watcher;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace DoubleJump
{

    [BepInPlugin("ShinyKelp.DoubleJump", "Double Jump", "1.0.0")]
    public partial class DoubleJumpMod : BaseUnityPlugin
    {

        private void OnEnable()
        {
            On.RainWorld.OnModsInit += RainWorldOnOnModsInit;
        }

        private bool IsInit;
        private bool hasImprovedInput = false;
        internal object djButton = null;
        private void RainWorldOnOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            try
            {
                if (IsInit) return;

                foreach (ModManager.Mod mod in ModManager.ActiveMods)
                {
                    if (mod.id == "improved-input-config")
                    {
                        hasImprovedInput = true;
                        RegisterDoubleJumpKeybind();
                        break;
                    }
                }

                On.Player.MovementUpdate += Player_Move;
                On.Player.Update += Player_Update;
                On.Player.ctor += Player_ctor;
                IL.Player.Jump += ReadIfCrouchJumped;
                On.Player.checkInput += Player_checkInput;
                IsInit = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
        }

        private void Player_checkInput(On.Player.orig_checkInput orig, Player self)
        {
            orig(self);
            if (!hasImprovedInput) return;
            
            if (CheckHeldInput(self) && self.canJump <= 0 && self.gravity != 0.0f 
                && (self.bodyMode == Player.BodyModeIndex.Default || self.bodyMode == Player.BodyModeIndex.Stand)
                && (!self.Shiny().hasDoubleJump || !self.Shiny().canWallDoubleJump))
                self.input[0].jmp = true;
        }

        const float topHeight = 9.5f;
        const float neutralHeight = 9f;
        const float neutralHeightOnCrouchNerf = -2.5f;
        const float neutralSpeedOnCrouchNerf = -1f;
        const float diagonalHeight = 8f;
        const float horizontalHeight = 4.5f;
        const float dropHeight = -1f;
        const float horizontalTopSpeed = 10f;
        const float horizontalMidSpeed = 7.5f;
        static private int djWindowCount = 0;
        const int djForbiddenWindow = 3;
        static private int noWallpounceCounter = 0;
        private const int noWallPounceWindow = 8;
        private static bool wallPounceToggled = false;

        private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            self.Shiny().hasDoubleJump = true;
            self.Shiny().canWallDoubleJump = true;
            wallPounceToggled = MMF.cfgWallpounce.Value;
        }

        private void ReadIfCrouchJumped(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.Index = 0;
            //Vertical leap
            c.GotoNext(MoveType.Before,
                x => x.MatchLdsfld<SoundID>("Slugcat_Crouch_Jump"));
            c.Index -= 6;
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<Player>>((self) => { self.Shiny().crouchJumped = true; });
        }

        private void Player_Move(On.Player.orig_MovementUpdate orig, Player self, bool eu)
        {
            orig.Invoke(self, eu);

            //Double Jump!
            if (self.Shiny().hasDoubleJump)
            {
                bool wantToDoubleJump = self.canJump <= 0f && IsPressingDoubleJumpButton(self);
                bool busyEating = self.eatMeat >= 20 || self.maulTimer >= 15;
                bool idleAction = self.bodyMode != Player.BodyModeIndex.Crawl && self.bodyMode != Player.BodyModeIndex.CorridorClimb &&
                    self.bodyMode != Player.BodyModeIndex.ClimbIntoShortCut && self.animation != Player.AnimationIndex.HangFromBeam &&
                    self.animation != Player.AnimationIndex.ClimbOnBeam && self.bodyMode != Player.BodyModeIndex.WallClimb &&
                    self.bodyMode != Player.BodyModeIndex.Swimming && self.animation != Player.AnimationIndex.AntlerClimb &&
                    self.animation != Player.AnimationIndex.VineGrab && self.animation != Player.AnimationIndex.ZeroGPoleGrab &&
                    self.animation != Player.AnimationIndex.CorridorTurn;

                bool usingGrappleworm = false;
                if (!MMF.cfgOldTongue.Value)
                {
                    for (int i = 0; i < self.grasps.Length; ++i)
                    {
                        if (self.grasps[i] != null && self.grasps[i].grabbed is TubeWorm tb && !tb.dead)
                            usingGrappleworm = true;
                    }
                }

                if (wantToDoubleJump && !busyEating && !usingGrappleworm &&
                    self.Consious && idleAction &&
                    self.onBack == null
                    && djWindowCount == 0)
                {
                    //Double jump attempt!
                    bool actuallyJumped = false;

                    //Double jump boost
                    float inputX = self.input[0].x;
                    float inputY = self.input[0].y;

                    //Straight up
                    //Tallest vertical reach.
                    //Forces stand.
                    //Resets horizontal momentum.
                    //Also useful for precise jumps.
                    if (inputX == 0f && inputY == 1f)
                    {
                        self.bodyChunks[0].vel.y = topHeight;
                        self.bodyChunks[1].vel.y = topHeight - 1f;
                        self.bodyChunks[0].vel.x = 0f;
                        self.bodyChunks[1].vel.x = 0f;
                        self.jumpBoost = 6f;
                        self.animation = Player.AnimationIndex.None;
                        self.bodyMode = Player.BodyModeIndex.Stand;
                        self.standing = true;
                        actuallyJumped = true;
                    }
                    //Straight down
                    //Resets horizontal momentum.
                    //Forces flip (does not change direction if already flipping).
                    //Useful for breaking falls.
                    else if (inputX == 0f && inputY == -1f && self.firstChunk.vel.y < -20f)
                    {
                        self.bodyChunks[0].vel.y = dropHeight;
                        self.bodyChunks[1].vel.y = dropHeight - 1f;
                        self.bodyChunks[0].vel.x = 0f;
                        self.bodyChunks[1].vel.x = 0f;
                        self.jumpBoost = 8f;
                        if (self.bodyChunks[0].vel.x != 0f && self.animation != Player.AnimationIndex.Flip)
                        {
                            self.animation = Player.AnimationIndex.Flip;
                            self.bodyMode = Player.BodyModeIndex.Default;
                            self.standing = false;
                            self.slideDirection = (self.bodyChunks[0].vel.x < 0 ? 1 : -1);
                        }
                        actuallyJumped = true;
                    }
                    //Neutral jump (no inputs)
                    //Second highest.
                    //Preserves horizontal momentum.
                    //Forces flip (does not change direction if already flipping).
                    //Best to combo with slide pounces and the like.
                    else if (inputX == 0 && inputY == 0)
                    {
                        self.bodyChunks[0].vel.y = neutralHeight;
                        self.bodyChunks[1].vel.y = neutralHeight;
                        if (self.Shiny().crouchJumped)
                        {
                            self.bodyChunks[0].vel.y += neutralHeightOnCrouchNerf;
                            self.bodyChunks[1].vel.y += neutralHeightOnCrouchNerf;
                            if (self.bodyChunks[0].vel.x != 0)
                            {
                                self.bodyChunks[0].vel.x += (neutralSpeedOnCrouchNerf * Mathf.Sign(self.bodyChunks[0].vel.x));
                                self.bodyChunks[1].vel.x += (neutralSpeedOnCrouchNerf * Mathf.Sign(self.bodyChunks[1].vel.x));
                            }
                        }
                        self.jumpBoost = 5f;
                        if (self.bodyChunks[0].vel.x != 0f && self.animation != Player.AnimationIndex.Flip)
                        {
                            self.animation = Player.AnimationIndex.Flip;
                            self.bodyMode = Player.BodyModeIndex.Default;
                            self.standing = false;
                            self.slideDirection = (self.bodyChunks[0].vel.x < 0 ? 1 : -1);
                        }
                        actuallyJumped = true;
                    }
                    //Diagonal
                    //Up variats are weaker.
                    //Up variants can maintain standing position.
                    //Down variants force forward flip.
                    //Useful for maneuvering mid-air.
                    else if (inputX != 0 && inputY != 0)
                    {
                        float flipBoostX = (inputY < 0 ? 0.5f : 0f);
                        float flipBoostY = (inputY < 0 ? 0.5f : 0f);

                        self.bodyChunks[0].vel.y = diagonalHeight + flipBoostY;
                        self.bodyChunks[1].vel.y = diagonalHeight + flipBoostY - 1f;
                        self.bodyChunks[0].vel.x = (horizontalMidSpeed + flipBoostX) * inputX;
                        self.bodyChunks[1].vel.x = (horizontalMidSpeed + flipBoostX - 2f) * inputX;

                        self.jumpBoost = 5f;
                        //Flip always with negative Y, flip always if player wasn't standing.
                        if (!self.standing || inputY < 0f)
                        {
                            self.animation = Player.AnimationIndex.Flip;
                            self.bodyMode = Player.BodyModeIndex.Default;
                            self.standing = false;
                            self.slideDirection = (int)-inputX;
                        }
                        actuallyJumped = true;
                    }
                    //Horizontal jump
                    //Fastest for horizontal travel.
                    //Forces forward flip.
                    else if (inputX != 0 && inputY == 0)
                    {
                        self.bodyChunks[0].vel.y = horizontalHeight;
                        self.bodyChunks[1].vel.y = horizontalHeight - 1f;
                        self.bodyChunks[0].vel.x = horizontalTopSpeed * inputX;
                        self.bodyChunks[1].vel.x = (horizontalTopSpeed - 2f) * inputX;
                        self.jumpBoost = 6f;
                        self.animation = Player.AnimationIndex.Flip;
                        self.bodyMode = Player.BodyModeIndex.Default;
                        self.standing = false;
                        self.slideDirection = (int)-inputX;
                        actuallyJumped = true;
                    }

                    if (actuallyJumped)
                    {
                        self.Shiny().hasDoubleJump = false;
                        //Add explosion sound and visual
                        int spikes = (self.Shiny().canWallDoubleJump ? 10 : 6);
                        float pitch = (self.Shiny().canWallDoubleJump ? 6f : 8.5f);
                        self.room.AddObject(new ExplosionSpikes(self.room, self.bodyChunks[1].pos, spikes, 15f, 10f, 12f, 17f, new Color(1f, 0.8f, 2f)));
                        self.room.PlaySound(SoundID.Flare_Bomb_Burn, self.firstChunk.pos, 0.8f, pitch);
                        self.Shiny().crouchJumped = false;
                        noWallpounceCounter = noWallPounceWindow;
                    }

                }
            }

            if (self.canJump > 0 || self.animation == Player.AnimationIndex.HangFromBeam ||
                self.animation == Player.AnimationIndex.ClimbOnBeam || self.bodyMode == Player.BodyModeIndex.WallClimb ||
                self.animation == Player.AnimationIndex.AntlerClimb || self.animation == Player.AnimationIndex.VineGrab ||
                self.animation == Player.AnimationIndex.ZeroGPoleGrab || self.bodyMode == Player.BodyModeIndex.Swimming)
            {
                if (self.bodyMode == Player.BodyModeIndex.WallClimb)
                {
                    if (self.Shiny().canWallDoubleJump && !self.Shiny().hasDoubleJump)
                    {
                        self.Shiny().canWallDoubleJump = false;
                        self.Shiny().hasDoubleJump = true;
                    }
                }
                else
                {
                    self.Shiny().canWallDoubleJump = true;
                    self.Shiny().hasDoubleJump = true;
                }
                self.Shiny().crouchJumped = false;
            }
            
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (self == null)
            {
                orig(self, eu);
                return;
            }

            /*
            bool idleAction = self.bodyMode != Player.BodyModeIndex.Crawl && self.bodyMode != Player.BodyModeIndex.CorridorClimb &&
                self.bodyMode != Player.BodyModeIndex.ClimbIntoShortCut && self.animation != Player.AnimationIndex.HangFromBeam &&
                self.animation != Player.AnimationIndex.ClimbOnBeam && self.bodyMode != Player.BodyModeIndex.WallClimb &&
                self.bodyMode != Player.BodyModeIndex.Swimming && self.animation != Player.AnimationIndex.AntlerClimb &&
                self.animation != Player.AnimationIndex.VineGrab && self.animation != Player.AnimationIndex.ZeroGPoleGrab &&
                self.animation != Player.AnimationIndex.CorridorTurn && self.animation != Player.AnimationIndex.StandOnBeam;
            */

            bool idleAction = self.bodyMode != Player.BodyModeIndex.Swimming && self.animation != Player.AnimationIndex.HangFromBeam
                && self.animation != Player.AnimationIndex.AntlerClimb && self.animation != Player.AnimationIndex.VineGrab && 
                self.animation != Player.AnimationIndex.ZeroGPoleGrab;

            if (!idleAction)
            {
                djWindowCount = djForbiddenWindow;

            }
            else if (djWindowCount > 0)
                djWindowCount--;

            if (noWallpounceCounter > 0)
            {
                MMF.cfgWallpounce.Value = false;
            }
            orig(self, eu);
            if (noWallpounceCounter > 0)
            {
                noWallpounceCounter--;
                if (noWallpounceCounter == 0)
                    MMF.cfgWallpounce.Value = wallPounceToggled;
            }
        }

        private void RegisterDoubleJumpKeybind()
        {
            djButton = PlayerKeybind.Register("DoubleJump:DoubleJumpButton", "Double Jump", "DoubleJump", KeyCode.C, KeyCode.JoystickButton0);
        }

        private bool IsPressingDoubleJumpButton(Player player)
        {
            if (!hasImprovedInput)
                return player.input[0].jmp && player.wantToJump > 0;
            else 
                return CheckJustPressedInput(player);
        }
        private bool CheckJustPressedInput(Player player)
        {
            return player.JustPressed(djButton as PlayerKeybind);
        }

        private bool CheckHeldInput(Player player)
        {
            return player.IsPressed(djButton as PlayerKeybind);
        }
    }
}
