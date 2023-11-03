using Godot;
using System;

public partial class PlasmagunWeapon : PlayerWeapon
{
	public override float avgDispersion { get { return .01f; } }
	public override float maxDispersion { get { return .02f; } }
	[Export]
	public string AttackProjectileName;
	public Vector3 spawnPos;
	protected override void OnUpdate()
	{
		if (playerInfo.Ammo[6] <= 0 && fireTime < .1f)
		{
			if ((!putAway) && (Sounds.Length > 1))
			{
				audioStream.Stream = Sounds[1];
				audioStream.Play();
			}
			putAway = true;
		}
	}
	protected override void OnInit()
	{
		if (Sounds.Length > 2)
		{
			audioStream.Stream = Sounds[2];
			audioStream.Play();
		}
	}
	public override bool Fire()
	{
		if (LowerAmount > .2f)
			return false;

		//small offset to allow continous fire animation
		if (fireTime > 0.05f)
			return false;

		if (playerInfo.Ammo[6] <= 0)
			return false;

		playerInfo.Ammo[6]--;

		if (GameOptions.UseMuzzleLight)
		{
			if (muzzleLight != null)
			{
				muzzleLight.LightEnergy = 1.0f;
				if (muzzleObject != null)
					if (!muzzleObject.Visible)
					{
						muzzleObject.Visible = true;
//						playerInfo.playerThing.avatar.MuzzleFlashSetActive(true);
					}
			}
		}
		//maximum fire rate 20/s, unless you use negative number (please don't)
		fireTime = _fireRate + .05f;
		coolTimer = 0f;

		if (Sounds.Length > 0)
		{
			audioStream.Stream = Sounds[0];
			audioStream.Play();
		}

		//Projectile attack
		{
			Transform3D global = playerInfo.playerCamera.CurrentCamera.GlobalTransform;
			Vector3 d = global.Basis.Z;
			Vector2 r = GetDispersion();
			d += global.Basis.X * r.X + global.Basis.Y * r.Y;
			d = d.Normalized();

			Projectile plasma = (Projectile)ThingsManager.thingsPrefabs[AttackProjectileName].Instantiate();
			GameManager.Instance.TemporaryObjectsHolder.AddChild(plasma);
			plasma.owner = playerInfo.playerThing;
			if (muzzleObject != null)
				plasma.GlobalPosition = muzzleObject.GlobalPosition;
			else
				plasma.GlobalPosition = playerInfo.playerCamera.CurrentCamera.GlobalPosition;
			plasma.LookAt(plasma.GlobalPosition - d, Vector3.Up);
		}

		return true;
	}
}