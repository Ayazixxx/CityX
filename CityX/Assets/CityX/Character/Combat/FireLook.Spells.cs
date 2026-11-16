using UnityEngine;

namespace CityX.Character
{
    /// <summary>What a fire spell's flame stream owes each layer of its fire (the parts of a puff left over between frames), kept by the stream.</summary>
    public struct FireJetDebts
    {
        public float Puffs, Core, Glow, Embers, Smoke, Splash, SplashGas, Sparks;
    }

    /// <summary>
    /// THE FIRE SPELLS (docs/FIRE.md, "The fire spells"; owner, 2026-11-12: "just make them all look better", with achrefelouafi's
    /// LinearAbiltyCastingThreeJS, MIT, as the reference): burning gas -- puffs of fire drawn by the fire shader's gas mode, cooling from
    /// white-hot through yellow, orange and red to smoke in bold bands -- in three shared systems (a fireball's wake, which narrows as it cools;
    /// a burst, which billows out and slows; the flame staff's stream, which swells as it flies), with sparks that streak, fall and bounce, and
    /// glowing cinders that cool to grey. Fed by hand as the ground fire is, so every fireball and stream in the city is a handful of draws.
    /// </summary>
    public sealed partial class FireLook
    {
        public const float CinderBrightness = 2.2f;

        private ParticleSystem _gas;
        private ParticleSystem _blast;
        private ParticleSystem _jet;
        private ParticleSystem _sparks;
        private ParticleSystem _cinders;

        private void MakeSpellSystems(Transform parent)
        {
            Material gas = FireSprites.Gas();
            // Stretched along their flight, each puff a tongue of fire (the gas mode of CityX/Fire draws it so): the wake's lick back off the
            // ball, the burst's fly out and round off as they slow, the stream's run together into one body of fire.
            _gas = Make(parent, "Gas", gas, 2500, ParticleSystemRenderMode.Stretch, true);
            _blast = Make(parent, "Blast", gas, 1200, ParticleSystemRenderMode.Stretch, true);
            _jet = Make(parent, "Jet", gas, 1500, ParticleSystemRenderMode.Stretch, true);
            Stretch(_gas, SpellFire.WakeStretch, 0.0f);
            Stretch(_blast, SpellFire.BlastStretch, SpellFire.BlastStretchPerSpeed);
            Stretch(_jet, SpellFire.JetStretch, SpellFire.JetStretchPerSpeed);
            _sparks = Make(parent, "Sparks", FireSprites.Particles("FireSparks", FireSprites.Dot(), ParticleBlend.Additive, 3, 2.2f), 800,
                ParticleSystemRenderMode.Stretch, false);
            _cinders = Make(parent, "Cinders", FireSprites.Particles("FireCinders", Texture2D.whiteTexture, ParticleBlend.Alpha, 0, CinderBrightness), 300,
                ParticleSystemRenderMode.Mesh, false);
            Grow(_gas, new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(0.5f, 0.78f), new Keyframe(1.0f, 0.55f)));
            Grow(_blast, new AnimationCurve(new Keyframe(0.0f, 0.45f, 4.0f, 4.0f), new Keyframe(0.35f, 1.1f), new Keyframe(1.0f, 1.5f)));
            Grow(_jet, new AnimationCurve(new Keyframe(0.0f, 0.55f), new Keyframe(0.35f, 1.2f), new Keyframe(1.0f, 2.6f)));
            Slow(_blast, 0.12f);
            Turbulent(_gas, 0.3f, 0.7f);
            Turbulent(_blast, 0.4f, 0.5f);
            Turbulent(_jet, 0.5f, 0.45f);
            StyleSparks(_sparks);
            StyleCinders(_cinders);
        }

        private void ClearSpellSystems()
        {
            _gas.Clear(true);
            _blast.Clear(true);
            _jet.Clear(true);
            _sparks.Clear(true);
            _cinders.Clear(true);
        }

        /// <summary>
        /// A fireball's wake over the stretch it flew this frame, <paramref name="from"/> to <paramref name="to"/>: puffs of burning gas left
        /// all along it (the faster it flies, the more, so it is as thick at any frame rate), drifting back and up and narrowing as they cool;
        /// embers, sparks and smoke shed off it. <paramref name="size"/> is the ball's against a plain one; <paramref name="debt"/> the ball's own.
        /// </summary>
        public void Wake(Vector3 from, Vector3 to, Vector3 heading, float size, ref float debt, float deltaTime)
        {
            // Green 0: each tongue's head trails its drift back off the ball (the gas mode of CityX/Fire), so its flame streams back from it.
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = new Color(1.0f, 0.0f, 1.0f, 1.0f) };
            int puffs = SpellFire.WakePuffs(Vector3.Distance(from, to), size, ref debt);
            for (int i = 0; i < puffs; i++)
            {
                // Led forward so each tongue's head wraps the ball rather than starting a ball's width behind it.
                emit.position = Vector3.Lerp(from, to, (i + Random.value) / puffs) + heading * (SpellFire.WakeLeadMetres * size)
                    + Random.insideUnitSphere * (0.06f * size);
                emit.velocity = -heading * Random.Range(0.4f, 1.6f) + Vector3.up * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.4f;
                emit.startSize = Random.Range(0.85f, 1.15f) * SpellFire.WakePuffMetres * size;
                emit.startLifetime = Random.Range(SpellFire.WakeShortestSeconds, SpellFire.WakeLongestSeconds);
                emit.rotation = Random.Range(0.0f, 360.0f);
                _gas.Emit(emit, 1);
            }
            if (Random.value < SpellFire.WakeEmbersPerSecond * size * deltaTime)
            {
                EmitEmber(Vector3.Lerp(from, to, Random.value), -heading * 1.5f + Vector3.up * 0.6f, 1.0f);
            }
            if (Random.value < SpellFire.WakeSparksPerSecond * size * deltaTime)
            {
                EmitSpark(to, -heading * Random.Range(2.0f, 5.0f) + Random.insideUnitSphere * 2.5f, Random.Range(0.25f, 0.5f));
            }
            if (Random.value < SpellFire.WakeSmokePerSecond * size * deltaTime)
            {
                // Born at the ball, but the smoke shows only once it is a little old: the wake turns to smoke a few metres behind.
                EmitSmoke(to, 0.8f, 0.55f * size);
            }
        }

        /// <summary>
        /// A fireball's burst at <paramref name="at"/>, on a surface facing <paramref name="normal"/>: a white-hot flash, puffs of fire thrown out
        /// and away from the surface, billowing and slowing as they burn down to smoke, sparks flung out to fall and bounce, embers, glowing
        /// cinders that cool to grey where they land, a little smoke rising, and on a floor a moment of flames licking up.
        /// </summary>
        public void Burst(Vector3 at, Vector3 normal, float size)
        {
            Vector3 away = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            float reach = Mathf.Sqrt(size);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = Color.white };
            Vector3 middle = at + away * (0.3f * size);
            for (int i = 0; i < 3; i++)
            {
                emit.position = middle + Random.insideUnitSphere * 0.1f * size;
                emit.velocity = (away + Random.insideUnitSphere * 0.5f) * 0.3f;   // barely moving, but a way to be stretched along
                emit.startSize = Random.Range(1.2f, 1.6f) * size;
                emit.startLifetime = Random.Range(0.14f, 0.2f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                _blast.Emit(emit, 1);
            }
            int puffs = SpellFire.Scaled(SpellFire.BurstPuffs, size);
            for (int i = 0; i < puffs; i++)
            {
                Vector3 direction = Outward(away, 0.95f);
                emit.position = middle + direction * Random.Range(0.0f, 0.3f) * size;
                emit.velocity = direction * Random.Range(3.0f, 7.5f) * reach + Vector3.up * Random.Range(0.3f, 1.2f);
                emit.startSize = Random.Range(0.8f, 1.3f) * size;
                emit.startLifetime = Random.Range(0.45f, 0.85f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                _blast.Emit(emit, 1);
            }
            int sparks = SpellFire.Scaled(SpellFire.BurstSparks, size);
            for (int i = 0; i < sparks; i++)
            {
                EmitSpark(middle, (Outward(away, 1.1f) + Vector3.up * 0.3f) * Random.Range(5.0f, 12.0f) * reach, Random.Range(0.35f, 0.9f));
            }
            int embers = SpellFire.Scaled(SpellFire.BurstEmbers, size);
            for (int i = 0; i < embers; i++)
            {
                EmitEmber(middle + Random.insideUnitSphere * 0.3f * size, Outward(away, 1.0f) * Random.Range(1.5f, 4.5f) * reach, 1.0f);
            }
            int cinders = SpellFire.Scaled(SpellFire.BurstCinders, size);
            for (int i = 0; i < cinders; i++)
            {
                Vector3 thrown = (Outward(away, 1.0f) + Vector3.up * 0.6f).normalized;
                float chunk = Random.Range(0.05f, 0.11f) * reach;
                emit.position = middle;
                emit.velocity = thrown * Random.Range(4.0f, 8.0f) * reach;
                emit.startSize3D = new Vector3(chunk, chunk * Random.Range(0.6f, 1.0f), chunk * Random.Range(0.7f, 1.2f));
                emit.rotation3D = new Vector3(Random.Range(0.0f, 360.0f), Random.Range(0.0f, 360.0f), Random.Range(0.0f, 360.0f));
                emit.startLifetime = Random.Range(1.6f, 2.6f);
                _cinders.Emit(emit, 1);
            }
            int smoke = SpellFire.Scaled(SpellFire.BurstSmoke, size);
            for (int i = 0; i < smoke; i++)
            {
                EmitSmoke(middle + Vector3.up * (0.5f * size) + Random.insideUnitSphere * 0.4f * size, 1.0f, Random.Range(1.0f, 1.6f) * size);
            }
            if (away.y > 0.5f)
            {
                float debt = 0.0f;
                EmitFlames(at, 1.0f, 0.9f * size, ref debt, 3.0f + 3.0f * SpellFire.Charge(size), 0.35f * size);
            }
        }

        /// <summary>
        /// One frame of a flame staff's stream from <paramref name="from"/> along <paramref name="direction"/>, reaching <paramref name="reach"/>
        /// metres when nothing is in the way and ending <paramref name="end"/> metres out: puffs of burning gas flying out and swelling, a white-hot
        /// core at the staff, a soft glow along it, embers, smoke rising off its end; and where it meets something (<paramref name="hit"/>, at
        /// <paramref name="hitPoint"/> facing <paramref name="hitNormal"/>) it dies against it and splashes: flames licking up, gas spreading
        /// over the surface, sparks. <paramref name="strength"/> is the rain's toll (1 dry), and how hard it burns (a jetpack's hover, less).
        /// <paramref name="scale"/> is its size against the staff's (the flamethrower's, a jetpack's exhaust), and <paramref name="carried"/>
        /// the velocity of what it pours from, which the gas keeps (a jetpack's flame stays under the flyer instead of streaming off behind).
        /// </summary>
        public void Jet(Vector3 from, Vector3 direction, float reach, float end, bool hit, Vector3 hitPoint, Vector3 hitNormal, float strength,
            float scale, Vector3 carried, ref FireJetDebts debts, float deltaTime)
        {
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            float fullLife = reach / SpellFire.JetSpeed;
            debts.Puffs += SpellFire.JetPuffsPerSecond * strength * deltaTime;
            while (debts.Puffs >= 1.0f)
            {
                debts.Puffs -= 1.0f;
                Vector3 spread = (direction + Random.insideUnitSphere * 0.07f).normalized;
                float start = Random.Range(0.0f, 0.3f) * scale;
                float life = SpellFire.JetLife(fullLife, end - start);
                emit.position = from + direction * start;
                emit.velocity = spread * SpellFire.JetSpeed * Random.Range(0.92f, 1.08f) + carried;
                emit.startSize = Random.Range(0.85f, 1.15f) * SpellFire.JetPuffMetres * scale;
                emit.startLifetime = life;
                emit.rotation = Random.Range(0.0f, 360.0f);
                emit.startColor = new Color(life / fullLife, 1.0f, 1.0f, strength);
                _jet.Emit(emit, 1);
            }
            debts.Core += SpellFire.JetCorePerSecond * deltaTime;
            while (debts.Core >= 1.0f)
            {
                // The white-hot core at the staff: short-lived puffs that are only the first stretch of a full life, so they die still hot.
                debts.Core -= 1.0f;
                float life = SpellFire.JetLife(SpellFire.JetCoreSeconds, end);
                emit.position = from + direction * Random.Range(0.0f, 0.15f) * scale;
                emit.velocity = direction * SpellFire.JetSpeed + carried;
                emit.startSize = Random.Range(0.18f, 0.26f) * scale;
                emit.startLifetime = life;
                emit.rotation = Random.Range(0.0f, 360.0f);
                emit.startColor = new Color(life / fullLife, 1.0f, 1.0f, 1.0f);
                _jet.Emit(emit, 1);
            }
            debts.Glow += SpellFire.JetGlowPerSecond * strength * deltaTime;
            while (debts.Glow >= 1.0f)
            {
                debts.Glow -= 1.0f;
                emit.position = from + direction * (end * Random.Range(0.15f, 0.85f));
                emit.velocity = direction * 2.0f + carried;
                emit.startSize = Random.Range(1.4f, 2.0f) * scale;
                emit.startLifetime = 0.4f;
                emit.startColor = new Color(GlowColour.r, GlowColour.g, GlowColour.b, 0.3f * strength);
                _glow.Emit(emit, 1);
            }
            debts.Embers += SpellFire.JetEmbersPerSecond * strength * scale * deltaTime;
            while (debts.Embers >= 1.0f)
            {
                debts.Embers -= 1.0f;
                EmitEmber(from + direction * (end * Random.Range(0.3f, 1.0f)), direction * Random.Range(1.0f, 4.0f) * scale + Vector3.up * 0.8f, 1.0f);
            }
            debts.Smoke += SpellFire.JetSmokePerSecond * scale * deltaTime;
            while (debts.Smoke >= 1.0f)
            {
                debts.Smoke -= 1.0f;
                EmitSmoke(from + direction * (end * Random.Range(0.75f, 1.0f)) + Vector3.up * 0.3f * scale, 1.0f, Random.Range(0.8f, 1.3f) * scale);
            }
            if (!hit)
            {
                return;
            }
            Vector3 away = hitNormal.sqrMagnitude > 1e-6f ? hitNormal.normalized : -direction;
            // A small stream's splash burns less hot and fewer (a jetpack's exhaust on the street: a lick of flame, not a bonfire).
            EmitFlames(hitPoint + away * 0.05f, strength * Mathf.Lerp(0.55f, 1.0f, scale), 0.75f * scale, ref debts.Splash,
                SpellFire.SplashFlamesPerSecond * strength * Mathf.Sqrt(scale) * deltaTime, 0.45f * scale);
            debts.SplashGas += SpellFire.SplashGasPerSecond * strength * deltaTime;
            while (debts.SplashGas >= 1.0f)
            {
                debts.SplashGas -= 1.0f;
                Vector3 over = Vector3.ProjectOnPlane(Random.insideUnitSphere + direction * 0.8f, away);
                emit.position = hitPoint + away * 0.25f * scale;
                emit.velocity = (over.normalized * Random.Range(2.0f, 4.0f) + away * Random.Range(0.4f, 1.2f) + Vector3.up * 0.6f) * Mathf.Sqrt(scale);
                emit.startSize = Random.Range(0.6f, 0.9f) * scale;
                emit.startLifetime = Random.Range(0.3f, 0.5f);
                emit.rotation = Random.Range(0.0f, 360.0f);
                emit.startColor = new Color(1.0f, 1.0f, 1.0f, 0.85f * strength);
                _blast.Emit(emit, 1);
            }
            debts.Sparks += SpellFire.SplashSparksPerSecond * strength * deltaTime;
            while (debts.Sparks >= 1.0f)
            {
                debts.Sparks -= 1.0f;
                EmitSpark(hitPoint + away * 0.1f, (Outward(away, 1.2f) + Vector3.up * 0.4f) * Random.Range(3.0f, 7.0f) * Mathf.Sqrt(scale), Random.Range(0.3f, 0.6f));
            }
        }

        /// <summary>
        /// A ball of fire forming at a staff's head as its shot is charged: puffs of gas drawn in from round it and shrinking as they gather,
        /// more and hotter with <paramref name="charge"/> (0..1), and small flames licking up off it.
        /// </summary>
        public void Gather(Vector3 at, float charge, ref float debt, float deltaTime)
        {
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            debt += (18.0f + 40.0f * charge) * deltaTime;
            while (debt >= 1.0f)
            {
                debt -= 1.0f;
                Vector3 round = Random.onUnitSphere * Random.Range(0.35f, 0.6f) * (0.6f + 0.6f * charge);
                emit.position = at + round;
                emit.velocity = -round * 4.0f;
                emit.startSize = Random.Range(0.25f, 0.4f) * (0.6f + 0.8f * charge);
                emit.startLifetime = 0.22f;
                emit.rotation = Random.Range(0.0f, 360.0f);
                emit.startColor = new Color(0.4f, 1.0f, 1.0f, 0.55f + 0.45f * charge);   // gathering, it stays hot: only the first part of a life
                _gas.Emit(emit, 1);
            }
            Lick(at, at + Vector3.up * 0.05f, 0.4f + 0.6f * charge, 0.5f + 1.2f * charge, deltaTime);
        }

        /// <summary>A direction out of a surface facing <paramref name="away"/>: round it, <paramref name="spread"/> wide, never into the surface.</summary>
        private static Vector3 Outward(Vector3 away, float spread)
        {
            Vector3 direction = away + Random.insideUnitSphere * spread;
            if (Vector3.Dot(direction, away) < 0.1f)
            {
                direction -= away * (Vector3.Dot(direction, away) - 0.1f);
            }
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : away;
        }

        private void EmitSpark(Vector3 at, Vector3 velocity, float seconds)
        {
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false, position = at, velocity = velocity, startSize = Random.Range(0.04f, 0.08f), startLifetime = seconds,
            };
            _sparks.Emit(emit, 1);
        }

        /// <summary>
        /// Draws each puff <paramref name="length"/> times as long as it is wide along its flight, and longer the faster it flies
        /// (<paramref name="perSpeed"/> a metre a second). Never thinner when seen end on (freeform), so a wake seen from behind the caster is not
        /// a line.
        /// </summary>
        private static void Stretch(ParticleSystem particles, float length, float perSpeed)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.lengthScale = length;
            renderer.velocityScale = perSpeed;
            renderer.cameraVelocityScale = 0.0f;
            renderer.freeformStretching = true;
            renderer.rotateWithStretchDirection = true;
        }

        private static void Grow(ParticleSystem particles, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
        }

        /// <summary>Slows to <paramref name="share"/> of its speed over its life, fast at first: a burst that spends itself.</summary>
        private static void Slow(ParticleSystem particles, float share)
        {
            ParticleSystem.VelocityOverLifetimeModule drag = particles.velocityOverLifetime;
            drag.enabled = true;
            drag.speedModifier = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0.0f, 1.0f, -5.0f, -5.0f), new Keyframe(1.0f, share)));
        }

        private static void Turbulent(ParticleSystem particles, float strength, float frequency)
        {
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;
        }

        /// <summary>Streaks flung out, falling and bouncing, white-hot to orange to red as they die.</summary>
        private static void StyleSparks(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = 1.0f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.025f;
            renderer.lengthScale = 1.0f;
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1.0f, 0.95f, 0.8f), 0.0f), new GradientColorKey(new Color(1.0f, 0.6f, 0.15f), 0.4f), new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            Bounce(particles, 0.3f, 0.45f);
        }

        /// <summary>
        /// Chunks of molten rock, tumbling, bouncing where they land and cooling as they lie: white-hot, then the fire's bands (yellow, orange,
        /// red: a fixed gradient steps, as the cel look's bands do), then a dark grey cinder, gone at the end.
        /// </summary>
        private static void StyleCinders(ParticleSystem particles)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.mesh = RockChunk.Mesh;
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = 1.2f;
            main.startSize3D = true;
            main.startRotation3D = true;
            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-6.0f, 6.0f);
            spin.y = new ParticleSystem.MinMaxCurve(-6.0f, 6.0f);
            spin.z = new ParticleSystem.MinMaxCurve(-6.0f, 6.0f);
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            float shown = 1.0f / CinderBrightness;
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(SpellFire.BandColours[4] * shown, 0.08f), new GradientColorKey(SpellFire.BandColours[3] * shown, 0.22f),
                    new GradientColorKey(SpellFire.BandColours[2] * shown, 0.4f), new GradientColorKey(SpellFire.BandColours[1] * (shown * 0.7f), 0.62f),
                    new GradientColorKey(new Color(0.07f, 0.065f, 0.06f), 1.0f),
                },
                new[] { new GradientAlphaKey(1.0f, 0.85f), new GradientAlphaKey(0.0f, 1.0f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            Bounce(particles, 0.25f, 0.5f);
        }

        private static void Bounce(ParticleSystem particles, float bounce, float dampen)
        {
            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = bounce;
            collision.dampen = dampen;
            collision.lifetimeLoss = 0.1f;
        }
    }
}
