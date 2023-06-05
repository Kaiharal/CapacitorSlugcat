using System;
using BepInEx;
using UnityEngine;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using SlugBase;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions.Must;
using IL.Noise;
using SlugBase.DataTypes;
using IL.Music;
using JetBrains.Annotations;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using MonoMod.RuntimeDetour;
using ImprovedInput;
using Kittehface.Framework20;
using RWCustom;
using On;
using MoreSlugcats;
using System.Threading; 
using DevInterface;
using System.Linq;
using IL;
using Unity.Collections;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "The Capacitor", "1.0.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "kylira.capacitor";

        public static readonly PlayerFeature<float> OutputMode = PlayerFloat("capacitor/output_mode");
        public static readonly PlayerFeature<float> OutputJump = PlayerFloat("capacitor/output_jump");

        //this is making a new keybind
        public static readonly PlayerKeybind PowerSwitch = PlayerKeybind.Register("capacitor:Output Mode", "The Capacitor", "Toggle Output Mode", KeyCode.LeftControl, KeyCode.JoystickButton4);

        public void OnEnable()
        {
            On.Player.ctor += Player_Ctor;
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);
            On.Player.Update += charged_Form;
            On.Player.Jump += output_Jump;
            On.PlayerGraphics.DrawSprites += rgbEyes;
            On.Player.ThrownSpear += superSpear;
            On.Player.Grabbed += emergencyMeasures;
            On.Player.SlugcatGrab += spearRecharge;
            On.Centipede.Shock += centiImmunity;
            On.JellyFish.Collide += smallJellyCharge;
            On.MoreSlugcats.BigJellyFish.Collide += bigJellyBlowTheFuckUp;
            On.PlayerGraphics.DefaultSlugcatColor += Default_SlugcatColor;
            On.Player.Die += Player_Die;


        }



        public static float maxTimer = 400f;
        private void LoadResources(RainWorld rainWorld)
        {
            //this is mostly just here for a reminder if I want to add more later
        }
        public class Capdata
        {
            public bool poweredOn;
            public float timer;
            public bool shorted;
            public bool outputJump;
            public int bootlegTimer;
            public float shockhopCooldown;
            public bool fullCharge;

            public Capdata()
            {

                this.poweredOn = false;
                this.timer = maxTimer;
                this.shorted = false;
                this.outputJump = false;
                this.bootlegTimer = 0;
                this.shockhopCooldown = 0f;
                this.fullCharge = true;
            }
        }

        public static ConditionalWeakTable<Player, Capdata> capi = new();


        //----


        #region Scug

        private void charged_Form(On.Player.orig_Update orig, Player self, bool eu)   //main ability Function 
        {
            orig(self, eu);
            if(self == null)
            {
                Debug.LogWarning(">>>charged_form: Null Pass");
                return;
            }
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            var room = self.room;                //variable for sounds, 

            if (self.JustPressed(PowerSwitch))
            {
                togglePower(self);
            }    //pressing the PowerSwitch keybind will trigger c 

            if (c.poweredOn && self.slugcatStats.name.value == "capacitor")
            {
                if (c.timer > 0)
                {
                    c.timer--;
                }
                if (c.timer == ((maxTimer / 4) * 3)) //c and the next few funtions check for the quarter makrs of charge, then play sounds at those marks.
                {
                    soundEffects(2f, self, room);
                }
                if (c.timer == (maxTimer / 2))
                {
                    soundEffects(2f, self, room);
                }
                if (c.timer == (maxTimer / 4))
                {
                    soundEffects(2f, self, room);
                }
                if (c.timer == 25 || c.timer == 19 || c.timer == 13 || c.timer == 7 || c.timer == 5 || c.timer == 3 || c.timer == 1)
                {
                    soundEffects(3f, self, room);
                }
                if (c.timer <= 0 && !c.shorted)
                {
                    if (self.FoodInStomach >= 1) //refills 50% charge at the cost of a food pip
                    {
                        self.SubtractFood(1);
                        c.timer = maxTimer / 2;
                        room.PlaySound(SoundID.Centipede_Shock, self.mainBodyChunk, false, 1f, 0.5f);
                    }
                    else //or stuns you if there is no food 
                    {
                        c.shorted = true;
                        togglePower(self);
                        c.timer = -55f;
                        self.room.PlaySound(SoundID.Bomb_Explode, self.mainBodyChunk, false, 0.5f, 2f);
                        self.room.PlaySound(SoundID.Zapper_Zap, self.mainBodyChunk, false, 1f, 0.6f);
                        self.Stun(150);
                        for (int i = 10; i > 0; i--)
                        {
                            Vector2 a = Custom.RNV();
                            self.room.AddObject(new Spark(self.mainBodyChunk.pos + a * UnityEngine.Random.value * 40f, a * Mathf.Lerp(4f, 30f, UnityEngine.Random.value), Color.white, null, 8, 12));
                            self.room.AddObject(new Spark(self.mainBodyChunk.pos + a * UnityEngine.Random.value * 40f, a * Mathf.Lerp(4f, 30f, UnityEngine.Random.value), Color.grey, null, 8, 12));
                        }
                        self.room.AddObject(new Explosion.ExplosionSmoke(self.mainBodyChunk.pos, Custom.RNV() * 5f * UnityEngine.Random.value, 0.5f));
                    }
                }

            }    //makes the timer for the power tick down while active, and checks if the meter is empty.

            if (!c.poweredOn && self.slugcatStats.name.value == "capacitor")
            {
                if (c.timer < maxTimer)
                {
                    if (c.bootlegTimer < 4)
                    {
                        c.bootlegTimer++;
                    }
                    if (c.bootlegTimer == 4)
                    {
                        c.timer++;
                        c.bootlegTimer = 0;
                    }
                }
                if (c.timer == (maxTimer / 4) * 3)
                {
                    soundEffects(2f, self, room);
                    c.timer = c.timer + 1;
                }
                if (c.timer == (maxTimer / 2))
                {
                    soundEffects(2f, self, room);
                    c.timer = c.timer + 1;
                }
                if (c.timer == (maxTimer / 4))
                {
                    soundEffects(2f, self, room);
                    c.timer += 1;
                }
                if (c.timer == 399f && c.fullCharge == false)
                {
                    room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, self.mainBodyChunk, false, 3f, 2f);
                }
            }  //makes the power recharge while inactive

            if (c.timer == maxTimer)
            {
                c.shorted = false;
                c.fullCharge = true;
            }
            else
            {
                c.fullCharge = false;
            }

            // -----------------------------------------------------------------------ShockHop---------------------------------------------------------------------------------------

            void shockHop()
            {
                //modified from ClassAbilityArtificer
                if (self.slugcatStats.name.value == "capacitor" && self != null)
                {
                    bool flag = self.wantToJump > 0 && self.input[0].pckp && c.shockhopCooldown <= 0;
                    //if jump and grab are pressed at the same time. Probably will need to work out another keybind. maybe use a HELD DOWN jump input, for manditory delay?

                    if (c.shockhopCooldown > 0)
                    {
                        c.shockhopCooldown--;
                    }
                    if (c.poweredOn && flag && self.canJump <= 0 && (self.input[0].y >= 0 || (self.input[0].y < 0 && (self.bodyMode == Player.BodyModeIndex.ZeroG || self.gravity <= 0.1f))) && self.Consious && self.bodyMode != Player.BodyModeIndex.Crawl && self.bodyMode != Player.BodyModeIndex.CorridorClimb && self.bodyMode != Player.BodyModeIndex.ClimbIntoShortCut && self.animation != Player.AnimationIndex.HangFromBeam && self.animation != Player.AnimationIndex.ClimbOnBeam && self.bodyMode != Player.BodyModeIndex.WallClimb && self.bodyMode != Player.BodyModeIndex.Swimming && self.animation != Player.AnimationIndex.AntlerClimb && self.animation != Player.AnimationIndex.VineGrab && self.animation != Player.AnimationIndex.ZeroGPoleGrab && self.onBack == null)
                    //does all the checks to make sure shockHop doesn't happen in weird places
                    {
                        c.shockhopCooldown = 20f;
                        c.timer = c.timer - 75f; //subtracts the charge on jumping
                        self.noGrabCounter = 5;
                        Vector2 pos = self.firstChunk.pos;
                        for (int i = 0; i < 8; i++)
                        {
                            self.room.AddObject(new Explosion.ExplosionLight(self.firstChunk.pos, 25f, 0.5f, 4, new Color(235f / 255f, 217f / 255f, 63f / 255f)));
                            //does an electric visual effect on shockHopping
                        }
                        for (int j = 0; j < 10; j++)
                        {
                            Vector2 a = Custom.RNV();
                            self.room.AddObject(new Spark(pos + a * UnityEngine.Random.value * 40f, a * Mathf.Lerp(4f, 30f, UnityEngine.Random.value), Color.white, null, 4, 18));
                        }
                        self.room.PlaySound(SoundID.Zapper_Zap, self.mainBodyChunk, false, 1f, 2f);
                        if (self.bodyMode == Player.BodyModeIndex.ZeroG || self.room.gravity == 0f || self.gravity == 0f)//jump math for if in 0g
                        {
                            float num3 = (float)self.input[0].x;
                            float num4 = (float)self.input[0].y;
                            while (num3 == 0f && num4 == 0f)
                            {
                                num3 = (float)(((double)UnityEngine.Random.value <= 0.33) ? 0 : (((double)UnityEngine.Random.value <= 0.5) ? 1 : -1));
                                num4 = (float)(((double)UnityEngine.Random.value <= 0.33) ? 0 : (((double)UnityEngine.Random.value <= 0.5) ? 1 : -1));
                            }
                            self.bodyChunks[0].vel.x = 7f * num3;
                            self.bodyChunks[0].vel.y = 7f * num4;
                            self.bodyChunks[1].vel.x = 6f * num3;
                            self.bodyChunks[1].vel.y = 6f * num4;
                        }
                        else  //and for in normal gravity.
                        {
                            if (self.input[0].x != 0)
                            {
                                self.bodyChunks[0].vel.y = Mathf.Min(self.bodyChunks[0].vel.y, 0f) + 6f;
                                self.bodyChunks[1].vel.y = Mathf.Min(self.bodyChunks[1].vel.y, 0f) + 5f;
                                self.jumpBoost = 6f;
                            }
                            if (self.input[0].x == 0 || self.input[0].y == 1)
                            {
                                self.bodyChunks[0].vel.y = 10f;
                                self.bodyChunks[1].vel.y = 9f;
                                self.jumpBoost = 8f;
                            }
                            if (self.input[0].y == 1)
                            {
                                self.bodyChunks[0].vel.x = 8f * (float)self.input[0].x;
                                self.bodyChunks[1].vel.x = 8f * (float)self.input[0].x;
                            }
                            else
                            {
                                self.bodyChunks[0].vel.x = 10f * (float)self.input[0].x;
                                self.bodyChunks[1].vel.x = 9f * (float)self.input[0].x;
                            }
                            self.animation = Player.AnimationIndex.Flip;
                            self.bodyMode = Player.BodyModeIndex.Default;
                        }
                    }
                }
            }
            shockHop();

            waterDischarge(self);

        }
        private void waterDischarge(Player self)
        {
            if(self == null)
            {
                return;
            }
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception err)
            {
                Debug.Log(err);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            var room = self.room;

            if (self.animation == Player.AnimationIndex.SurfaceSwim || self.animation == Player.AnimationIndex.DeepSwim)
            {
                if (c.poweredOn && !c.shorted)
                {
                    self.room.AddObject(new UnderwaterShock(room, self, self.mainBodyChunk.pos, 5, 1000f, 10f, self, Color.yellow));
                    togglePower(self);
                    c.timer = 0;
                    c.shorted = true;
                    self.room.PlaySound(SoundID.Zapper_Zap, self.mainBodyChunk, false, 2, 0.5f);
                    self.Stun(250);
                }
                if (!c.poweredOn && !c.shorted)
                {
                    c.timer = 0;
                    c.shorted = true;
                    self.room.PlaySound(SoundID.Snail_Pop, self.mainBodyChunk, false, 2, 1.5f);
                }
                else
                {
                    c.timer = 0f;
                    c.shorted = true;
                }
            }
        }
        void togglePower(Player self)
        {
            if (self == null) 
            { return; }
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            var room = self.room;
            if (!c.poweredOn && c.shorted == false && self.slugcatStats.name.value == "capacitor")
            {
                c.poweredOn = true;
                if (self.slugcatStats.name.value == "capacitor")
                {
                    self.slugcatStats.runspeedFac = 2f;
                    self.slugcatStats.corridorClimbSpeedFac = 2f;
                    self.slugcatStats.poleClimbSpeedFac = 2f;
                    self.slugcatStats.throwingSkill = 2;
                    self.slugcatStats.generalVisibilityBonus = 0.5f;
                    self.slugcatStats.loudnessFac = 2f;
                    c.outputJump = true;
                    soundEffects(0f, self, room);
                    if (self.grasps[0].grabbed is ElectricSpear && (self.grasps[0].grabbed as ElectricSpear).abstractSpear.electricCharge == 0)
                    {
                        self.room.PlaySound(SoundID.Fire_Spear_Pop, self.mainBodyChunk, false, 1f, 1.5f);
                        (self.grasps[0].grabbed as ElectricSpear).electricColor = Custom.HSL2RGB(UnityEngine.Random.Range(0.1f, 0.165f), UnityEngine.Random.Range(0.8f, 1f), UnityEngine.Random.Range(0.3f, 0.6f));
                        (self.grasps[0].grabbed as ElectricSpear).abstractSpear.electricCharge = 1;
                    }
                    if (self.grasps[1].grabbed is ElectricSpear && (self.grasps[1].grabbed as ElectricSpear).abstractSpear.electricCharge == 0)
                    {
                        self.room.PlaySound(SoundID.Fire_Spear_Pop, self.mainBodyChunk, false, 1f, 1.5f);
                        (self.grasps[1].grabbed as ElectricSpear).electricColor = Custom.HSL2RGB(UnityEngine.Random.Range(0.1f, 0.165f), UnityEngine.Random.Range(0.8f, 1f), UnityEngine.Random.Range(0.3f, 0.6f));
                        (self.grasps[1].grabbed as ElectricSpear).abstractSpear.electricCharge = 1;
                    }

                }
            }
            else if (c.poweredOn)
            {
                c.poweredOn = false;
                if (self.slugcatStats.name.value == "capacitor")
                {
                    self.slugcatStats.runspeedFac = 0.9f;
                    self.slugcatStats.corridorClimbSpeedFac = 0.9f;
                    self.slugcatStats.poleClimbSpeedFac = 0.9f;
                    self.slugcatStats.throwingSkill = 0;
                    self.slugcatStats.generalVisibilityBonus = 0.1f;
                    self.slugcatStats.loudnessFac = 1.1f;
                    c.outputJump = false;
                    self.jumpBoost = 1f; //c is mostly just to make sure that the output_jump() doesnt accidentally carry over somehow 
                    soundEffects(1f, self, room);
                }
            }
            else if (!c.poweredOn && c.shorted)
            {
                self.room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, self.mainBodyChunk, false, 2.2f, 0.3f);
            }

        }   //toggles the ability on and off, also charges electric spears when toggled on. 
        void soundEffects(float soundID, Player self, Room room)
        {
            if(!(room != null && self != null))
            {
                return;
            }

            float soundNumber = soundID;
            if (soundNumber == 0f)
            {
                //power on noise
                room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, self.mainBodyChunk, false, 2f, 4f);

            }
            else if (soundNumber == 1f)
            {
                //power off noise
                room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, self.mainBodyChunk, false, 2f, 3.6f);
            }
            else if (soundNumber == 2f)
            {
                //meter changing noise
                room.PlaySound(SoundID.Death_Lightning_Spark_Spontaneous, self.mainBodyChunk, false, 1f, 1f);
            }
            else if (soundNumber == 3f)
            {
                //warning trill
                room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, self.mainBodyChunk, false, 2f, 5f); //sound id, bodychunk sound is played from, if it loops, volume, pitch.
            }
            else if (soundNumber == 4f)
            {
                //power depleted noise
                room.PlaySound(SoundID.Zapper_Zap, self.mainBodyChunk, false, 0.75f, 2f);
            }

        }    //plays sounds for ability toggles and timers
        private void Player_Ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            if(!(self != null && abstractCreature != null && world != null))
            {
                return;
            }

            if (self.slugcatStats.name.value == "capacitor")
            {
                capi.Add(self, new Capdata());
            }

        } //this and the CWT above it allow the game to have different variables per-player.
        public void rgbEyes(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        { //changes the color of the face baseed on outputmode and percent of the meter left, as well as handles a few other visual things.
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if(!(self != null && sLeaser != null && rCam != null && timeStacker != 0 && camPos != null))
            {
                return;
            }

            if (self.player is Player p)
            {
                try
                {
                    if (p.slugcatStats.name.value != "capacitor")
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    return;
                }
                if (!capi.TryGetValue(p, out Capdata capdata))
                {
                    return;
                }
                capi.TryGetValue(p, out Capdata c);

                #region Eyes
                Color baseColor = Color.HSVToRGB(0.15f, 0.82f, 0.85f);
                Color baseColorOff = Color.HSVToRGB(0.50f, 1f, 1f);
                var facecolor = baseColor;
                if (c.timer % 3 == 0 && c.poweredOn)
                {
                    facecolor = Color.yellow;
                }
                if (c.timer % 3 != 0 && c.poweredOn)
                {

                    if (c.timer > (maxTimer / 4) * 3)
                    {
                        facecolor = baseColorOff;
                    }
                    if (c.timer <= ((maxTimer / 4) * 3) && c.timer > (maxTimer / 2))
                    {
                        facecolor.r = baseColorOff.r - 0.25f;
                        facecolor.g = baseColorOff.g - 0.25f;
                        facecolor.b = baseColorOff.b - 0.25f;
                    }
                    if (c.timer <= (maxTimer / 2) && c.timer > (maxTimer / 4))
                    {
                        facecolor.r = baseColorOff.r - 0.5f;
                        facecolor.g = baseColorOff.g - 0.5f;
                        facecolor.b = baseColorOff.b - 0.5f;
                    }
                    if (c.timer <= (maxTimer / 4))
                    {
                        facecolor.r = baseColorOff.r - 0.75f;
                        facecolor.g = baseColorOff.g - 0.75f;
                        facecolor.b = baseColorOff.b - 0.75f;
                    }

                }
                if (!c.poweredOn)
                {
                    if (c.timer > ((maxTimer / 4) * 3))
                    {
                        facecolor = baseColor;
                    }
                    if (c.timer <= ((maxTimer / 4) * 3) && (c.timer > (maxTimer / 2)))
                    {
                        facecolor.r = baseColor.r - 0.25f;
                        facecolor.g = baseColor.g - 0.25f;
                        facecolor.b = baseColor.b - 0.25f;
                    }
                    if (c.timer <= (maxTimer / 2) && c.timer > (maxTimer / 4))
                    {
                        facecolor.r = baseColor.r - 0.5f;
                        facecolor.g = baseColor.g - 0.5f;
                        facecolor.b = baseColor.b - 0.5f;
                    }
                    if (c.timer <= (maxTimer / 4))
                    {
                        facecolor.r = baseColor.r - 0.75f;
                        facecolor.g = baseColor.g - 0.75f;
                        facecolor.b = baseColor.b - 0.75f;
                    }
                }
                #endregion


                if (self.player.slugcatStats.name.value == "capacitor")
                {
                    //this is where the color decided on gets assigned, and also handles tail stuff
                    sLeaser.sprites[9].color = facecolor;

                    sLeaser.sprites[2].MoveBehindOtherNode(sLeaser.sprites[1]);
                   // sLeaser.sprites[2].color = Color.white;
                }

            }
        }
        private void output_Jump(On.Player.orig_Jump playerjump, Player self)
        {

            if(!(playerjump != null && self != null))
            {
                return;
            }
            playerjump(self);
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            if (c.outputJump) //a bool set later to determine if you are jumping whil in output mode 
            {
                if (OutputJump.TryGet(self, out var power))
                {
                    self.jumpBoost += 2.5f;
                }
            }
        }  //increases regular jump height in output mode.
        private void superSpear(On.Player.orig_ThrownSpear orig, Player self, Spear spear)
        {
            if(!(self != null && spear != null))
            {
                return;
            }

            var room = self.room;
            orig(self, spear);
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            if (c.poweredOn == true)
            {
                spear.spearDamageBonus = 2f;
                c.timer = c.timer - 120f;
                //audio
                room.PlaySound(SoundID.Fire_Spear_Pop, self.mainBodyChunk);
                for (int i = 8; i > 0; i--)
                {
                    Vector2 a = Custom.RNV();
                    self.room.AddObject(new Spark(self.mainBodyChunk.pos + a * UnityEngine.Random.value * 40f, a * Mathf.Lerp(4f, 20f, UnityEngine.Random.value), Color.yellow, null, 4, 10));
                    self.room.AddObject(new Spark(self.mainBodyChunk.pos + a * UnityEngine.Random.value * 40f, a * Mathf.Lerp(4f, 20f, UnityEngine.Random.value), Color.white, null, 4, 10));
                }
                //add visual effect?new Spark(pos + a * Random.value * 40f, a * Mathf.Lerp(4f, 30f, Random.value), Color.white, null, 4, 18
            }
        }
        private void emergencyMeasures(On.Player.orig_Grabbed orig, Player self, Creature.Grasp grasp)
        {
            if(!(self != null && grasp != null))
            {
                return;
            }

            orig(self, grasp);
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            //modified from DroneMaster's prevent dying ability
            if (c.poweredOn == true && grasp.grabber is not Player && grasp.grabber is not Centipede) //blast those fools
            {
                if (self == null) return;
                if (self.room == null) return;

                self.room.PlaySound(SoundID.Zapper_Zap, self.mainBodyChunk);
                self.room.PlaySound(SoundID.Fire_Spear_Explode, self.mainBodyChunk, false, 0.25f, 2f);

                self.room.AddObject(new ShockWave(self.mainBodyChunk.pos, 100f, 0.04f, 10));
                self.room.AddObject(new Explosion(self.room, self, self.DangerPos, 7, 250f, 1.5f, 0f, 150f, 0.25f, self, 0f, 150f, 0.5f));
                self.room.InGameNoise(new Noise.InGameNoise(self.mainBodyChunk.pos, 9000f, self, 1f));

                self.room.AddObject(new ExplosionSpikes(self.room, self.mainBodyChunk.pos, 10, 4f, 1f, 3f, 2f, Color.yellow));
                self.room.AddObject(new Explosion.ExplosionLight(self.mainBodyChunk.pos, 50f, 0.8f, 6, Color.yellow));
                self.Stun(25);

                c.poweredOn = false;
                if (self.slugcatStats.name.value == "capacitor")
                {
                    self.slugcatStats.runspeedFac = 1f;
                    self.slugcatStats.corridorClimbSpeedFac = 1f;
                    self.slugcatStats.poleClimbSpeedFac = 1f;
                    self.slugcatStats.throwingSkill = 0;
                    self.slugcatStats.generalVisibilityBonus = 0.1f;
                    self.slugcatStats.loudnessFac = 1.1f;
                    c.outputJump = false;
                    self.jumpBoost = 1f;
                } //this is all from togglePower, but it cant be called from here, so...
                c.timer = 0f;
                c.shorted = true;
                self.Stun(30);
            }
        } //emergency discharge if powered on
        private void spearRecharge(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            if(!(self != null && obj != null))
            {
                return;
            }

            orig(self, obj, graspUsed);

            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);


            if (ModManager.MSC && obj is ElectricSpear && c.poweredOn && (obj as ElectricSpear).abstractSpear.electricCharge == 0)
            {
                self.room.PlaySound(SoundID.Fire_Spear_Pop, self.mainBodyChunk, false, 1f, 1.5f);
                (obj as ElectricSpear).electricColor = Custom.HSL2RGB(UnityEngine.Random.Range(0.1f, 0.165f), UnityEngine.Random.Range(0.8f, 1f), UnityEngine.Random.Range(0.3f, 0.6f));
                (obj as ElectricSpear).abstractSpear.electricCharge = 1;

            }

            return;
        } //recharges electric spears when picked up in power state, and chenges the color ;3 
        private void centiImmunity(On.Centipede.orig_Shock orig, Centipede self, PhysicalObject shockObj)
        {
            if(!(self != null && shockObj != null))
            {
                return;
            }
            if (shockObj is Player p)
            {

                try
                {
                    if (p.slugcatStats.name.value != "capacitor")
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    return;
                }
                if (!capi.TryGetValue(p, out Capdata capdata))
                {
                    return;
                }
                capi.TryGetValue(p, out Capdata c);


                if (p == null) return;
                if (p.room == null) return;

                p.room.PlaySound(SoundID.Centipede_Shock, p.mainBodyChunk, false, 2f, 0.9f);
                p.room.PlaySound(SoundID.Bomb_Explode, p.mainBodyChunk, false, 1f, 2f);

                p.room.AddObject(new ExplosionSpikes(p.room, p.mainBodyChunk.pos, 10, 4f, 1f, 3f, 2f, Color.yellow));
                p.room.AddObject(new Explosion.ExplosionLight(p.mainBodyChunk.pos, 50f, 0.8f, 6, Color.yellow));
                p.room.AddObject(new ShockWave(p.mainBodyChunk.pos, 100f, 0.04f, 10));
                p.room.AddObject(new Explosion(p.room, p, p.DangerPos, 7, 250f, 4f, 0f, 350f, 0.25f, p, 0f, 200f, 0.5f));
                p.room.InGameNoise(new Noise.InGameNoise(p.mainBodyChunk.pos, 9000f, p, 1f));
                if (c.timer >= 351f && !c.shorted)
                {
                    p.Die();
                }
                if (c.timer <= 350f || c.shorted)
                {
                    p.Stun(300);
                    c.timer = maxTimer;
                    if (c.poweredOn)
                    {
                        togglePower(p);
                    }
                }
                if (!self.Red)
                {
                    self.Die();
                }


            }
        }
        private void smallJellyCharge(On.JellyFish.orig_Collide orig, JellyFish self, PhysicalObject otherObject, int myChunk, int otherChunk)
        {
            if(!(self != null && otherObject != null))
            {
                return;
            }

            if (otherObject is Player player && self.Electric && player.slugcatStats.name.value == "capacitor")
            {
                try
                {
                    if (player.slugcatStats.name.value != "capacitor")
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    return;
                }
                if (!capi.TryGetValue(player, out Capdata capdata))
                {
                    return;
                }
                capi.TryGetValue(player, out Capdata c);


                if ((c.timer <= 350f || c.shorted) && self.electricCounter > 0 && self.electricCounter < 100)
                {
                    c.timer = c.timer + 200f;
                    if (c.timer > 400f)
                    {
                        c.timer = 400f;
                    }
                    player.room.PlaySound(SoundID.Centipede_Shock, player.mainBodyChunk, false, 1.5f, 3);
                    player.room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, player.mainBodyChunk, false, 1.5f, 1.4f);
                    player.room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, player.mainBodyChunk, false, 1.5f, 1.8f);
                    player.room.PlaySound(SoundID.King_Vulture_Tusk_Aim_Beep, player.mainBodyChunk, false, 1.5f, 1.5f);
                    self.electricCounter = 0;
                }

                else if (c.timer >= 351f && !c.shorted && self.electricCounter > 0 && self.electricCounter < 100)
                {
                    player.Die();
                    c.timer = 0f;
                    player.room.PlaySound(SoundID.Bomb_Explode, player.mainBodyChunk, false, 1f, 2f);
                    player.room.PlaySound(SoundID.Zapper_Zap, player.mainBodyChunk, false, 1f, 0.5f);
                }
            }
            else
            {
                orig(self, otherObject, myChunk, otherChunk);
            }
        }
        private void bigJellyBlowTheFuckUp(On.MoreSlugcats.BigJellyFish.orig_Collide orig, MoreSlugcats.BigJellyFish self, PhysicalObject otherObject, int myChunk, int otherChunk)
        {
            if(!(self != null && otherObject != null))
            {
                return;
            }

            if (otherObject is Player player && (myChunk == self.CoreChunk || (myChunk == 0 && otherObject.Submersion > 0.8f)) && player.slugcatStats.name.value == "capacitor")
            {
                try
                {
                    if (player.slugcatStats.name.value != "capacitor")
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    return;
                }
                if (!capi.TryGetValue(player, out Capdata capdata))
                {
                    return;
                }
                capi.TryGetValue(player, out Capdata c);

                player.Die();
                self.Die();
                //modified from SingularityBomb.Explode 

                player.room.AddObject(new SingularityBomb.SparkFlash(player.mainBodyChunk.pos, 300f, new Color(0f, 0f, 1f)));
                player.room.AddObject(new Explosion(player.room, player, player.mainBodyChunk.pos, 7, 450f, 6.2f, 10f, 280f, 0.25f, player, 0.3f, 160f, 1f));
                player.room.AddObject(new Explosion(player.room, player, player.mainBodyChunk.pos, 7, 2000f, 4f, 0f, 400f, 0.25f, player, 0.3f, 200f, 1f));
                player.room.AddObject(new Explosion.ExplosionLight(player.mainBodyChunk.pos, 450f, 2f, 7, Color.cyan));
                player.room.AddObject(new Explosion.ExplosionLight(player.mainBodyChunk.pos, 230f, 1f, 3, new Color(1f, 1f, 1f)));
                player.room.AddObject(new Explosion.ExplosionLight(player.mainBodyChunk.pos, 1000f, 1f, 60, Color.yellow));
                player.room.AddObject(new ShockWave(player.mainBodyChunk.pos, 350f, 0.485f, 300, true));
                player.room.AddObject(new ShockWave(player.mainBodyChunk.pos, 2000f, 0.185f, 180, false));
                player.room.PlaySound(SoundID.Bomb_Explode, player.mainBodyChunk, false, 1.5f, 2f);
                player.room.PlaySound(SoundID.Bomb_Explode, player.mainBodyChunk, false, 1.5f, 1f);
                player.room.PlaySound(SoundID.Bomb_Explode, player.mainBodyChunk, false, 1.5f, 0.5f);

            }
            orig(self, otherObject, myChunk, otherChunk);
        }
        private Color Default_SlugcatColor(On.PlayerGraphics.orig_DefaultSlugcatColor orig, SlugcatStats.Name i)
        {

            if (i.value == "capacitor")
            {
                return Color.HSVToRGB(.983333f, 0.74f, 0.52f);

            }
            else
            {
                return orig(i);
            }
        }
        private void Player_Die(On.Player.orig_Die orig, Player self)
        {
            if(self == null)
            {
                return;
            }

            orig(self);
            try
            {
                if (self.slugcatStats.name.value != "capacitor")
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            if (!capi.TryGetValue(self, out Capdata capdata))
            {
                return;
            }
            capi.TryGetValue(self, out Capdata c);

            if (c.poweredOn)
            {
                togglePower(self);
            }

        }

        #endregion

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------









    }

}
 