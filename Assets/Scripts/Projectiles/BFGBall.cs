using Godot;
using System;
using System.Collections.Generic;
using ExtensionMethods;

public partial class BFGBall : InterpolatedNode3D
{
	public Node3D owner;
	[Export]
	public ParticlesController fx;
	[Export]
	public Node3D[] boltOrigin;
	[Export]
	PackedScene Bolt;
	public List<LightningBolt> lightningBolt = new List<LightningBolt>();
	[Export]
	public string projectileName;
	[Export]
	public bool destroyAfterUse = true;
	[Export]
	public float _lifeTime = 1;
	[Export]
	public float speed = 4f;
	[Export]
	public int rotateSpeed = 0;
	[Export]
	public int damageMin = 3;
	[Export]
	public int damageMax = 24;
	[Export]
	public int electricDamageMin = 6;
	[Export]
	public int electricDamageMax = 9;
	[Export]
	public int blastDamage = 0;
	[Export]
	public float projectileRadius = .2f;
	[Export]
	public float lightningRadius = 24f;
	[Export]
	public float explosionRadius = 2f;
	[Export]
	public float electricDamageRate = .05f;
	[Export]
	public DamageType damageType = DamageType.Generic;
	[Export]
	public float pushForce = 0f;
	[Export]
	public string OnDeathSpawn;
	[Export]
	public string decalMark;
	[Export]
	public string secondaryMark;
	[Export]
	public string SecondaryOnDeathSpawn;
	[Export]
	public string[] _humSounds;
	[Export]
	public string _onDeathSound;
	[Export]
	public MultiAudioStream audioStream;
	public AudioStreamWav[] humSounds;

	public uint ignoreSelfLayer = 0;

	float time = 0f;
	private float damageTime =  0;
	private Color Green = new Color(0x006158FF);
	private Rid Sphere;
	private PhysicsShapeQueryParameters3D SphereCast;
	private PhysicsPointQueryParameters3D PointIntersect;
	public enum CurrentHum
	{
		None,
		Idle,
		Fire
	}

	private CurrentHum currentHum = CurrentHum.None;
	public override void _Ready()
	{
		humSounds = new AudioStreamWav[_humSounds.Length];
		for (int i = 0; i < _humSounds.Length; i++)
			humSounds[i] = SoundManager.LoadSound(_humSounds[i], true);

		audioStream.Stream = humSounds[0];
		audioStream.Play();
		currentHum = CurrentHum.Idle;

		Sphere = PhysicsServer3D.SphereShapeCreate();
		SphereCast = new PhysicsShapeQueryParameters3D();
		SphereCast.ShapeRid = Sphere;

		PointIntersect = new PhysicsPointQueryParameters3D();
		PointIntersect.CollideWithAreas = true;
		PointIntersect.CollideWithBodies = false;
		PointIntersect.CollisionMask = (1 << GameManager.FogLayer);

		LightningBolt lightBolt = (LightningBolt)Bolt.Instantiate();
		lightBolt.boltMaterial.SetShaderParameter("lightning_color", Green);
		lightBolt.SetArcsLayers(GameManager.AllPlayerViewMask);
		lightningBolt.Add(lightBolt);
		boltOrigin[0].AddChild(lightningBolt[0]);
	}
	public void EnableQuad()
	{
		damageMin *= GameManager.Instance.QuadMul;
		damageMax *= GameManager.Instance.QuadMul;
		electricDamageMin *= GameManager.Instance.QuadMul;
		electricDamageMax *= GameManager.Instance.QuadMul;
		blastDamage *= GameManager.Instance.QuadMul;
		pushForce *= GameManager.Instance.QuadMul;
	}
	public override void _PhysicsProcess(double delta)
	{
		if (GameManager.Paused)
			return;

		float deltaTime = (float)delta;

		time += deltaTime;

		if (time >= _lifeTime)
		{
			if (!string.IsNullOrEmpty(_onDeathSound))
				SoundManager.Create3DSound(GlobalPosition, SoundManager.LoadSound(_onDeathSound));
			QueueFree();
			return;
		}

		if (damageTime > 0)
			damageTime -= deltaTime;

		CollisionObject3D Hit = null;
		Vector3 Collision = Vector3.Zero;
		Vector3 Normal = Vector3.Zero;
		Vector3 d = GlobalTransform.ForwardVector();

		var SpaceState = GetWorld3D().DirectSpaceState;
		//check for collision
		{
			PhysicsServer3D.ShapeSetData(Sphere, projectileRadius);
			SphereCast.CollisionMask = ~(GameManager.NoHitMask | ignoreSelfLayer);
			SphereCast.Motion = -d * speed * deltaTime;
			SphereCast.Transform = GlobalTransform;
			var result = SpaceState.CastMotion(SphereCast);

			if (result[1] < 1)
			{
				SphereCast.Transform = new Transform3D(GlobalTransform.Basis, GlobalTransform.Origin + (SphereCast.Motion * result[1]));
				var hit = SpaceState.GetRestInfo(SphereCast);
				if (hit.Count > 0)
				{
					CollisionObject3D collider = (CollisionObject3D)InstanceFromId((ulong)hit["collider_id"]);
					if (collider != owner)
					{
						Collision = (Vector3)hit["point"];
						Normal = (Vector3)hit["normal"];
						Hit = collider;
						Vector3 impulseDir = d.Normalized();

						if (Hit is Damageable damageable)
						{
							damageable.Damage(GD.RandRange(damageMin, damageMax) * 100, damageType, owner);
							damageable.Impulse(impulseDir, pushForce);
						}
					}
				}
			}
		}

		if (Hit == null)
		{
			PhysicsServer3D.ShapeSetData(Sphere, lightningRadius);
			SphereCast.CollisionMask = GameManager.TakeDamageMask;
			SphereCast.Motion = Vector3.Zero;
			SphereCast.Transform = GlobalTransform;
			var hits = SpaceState.IntersectShape(SphereCast);
			var max = hits.Count;

			int damaged = 0;
			for (int i = 0; i < max; i++)
			{
				var hit = hits[i];

				CollisionObject3D posibleCollider = (CollisionObject3D)hit["collider"];
				if (posibleCollider == owner)
					continue;

				if (posibleCollider is Damageable)
				{
					Vector3 collision = posibleCollider.Position;
					var RayCast = PhysicsRayQueryParameters3D.Create(GlobalPosition, collision, ((1 << GameManager.ColliderLayer) | GameManager.TakeDamageMask));
					var check = SpaceState.IntersectRay(RayCast);
					if (check.Count > 0)
					{
						CollisionObject3D collider = (CollisionObject3D)check["collider"];
						if (collider == owner)
							continue;

						if (collider is Damageable damageable)
						{
							boltOrigin[damaged].Show();
							boltOrigin[damaged].LookAt(collision);
							if (damaged == lightningBolt.Count)
							{
								LightningBolt lightBolt = (LightningBolt)Bolt.Instantiate();
								lightBolt.boltMaterial.SetShaderParameter("lightning_color", Green);
								lightBolt.SetArcsLayers(GameManager.AllPlayerViewMask);
								lightningBolt.Add(lightBolt);
								boltOrigin[damaged].AddChild(lightningBolt[damaged]);
							}
							lightningBolt[damaged].SetBoltMesh(GlobalPosition, collision);
							if (damageTime <= 0)
								damageable.Damage(GD.RandRange(electricDamageMin, electricDamageMax), DamageType.Lightning, owner);
							damaged++;
						}
					}
				}

			}
			if (damaged > 0)
			{
				if (damageTime <= 0)
					damageTime = electricDamageRate + .05f;
				if (currentHum != CurrentHum.Fire)
				{
					audioStream.Stream = humSounds[1];
					audioStream.Play();
					currentHum = CurrentHum.Fire;
				}
				for (int i = damaged; i < boltOrigin.Length; i++)
					boltOrigin[i].Hide();
			}
			else
			{
				if (currentHum != CurrentHum.Idle)
				{
					audioStream.Stream = humSounds[0];
					audioStream.Play();
					currentHum = CurrentHum.Idle;
				}
				for (int i = 0; i < boltOrigin.Length; i++)
					boltOrigin[i].Hide();
			}
		}
		//explosion
		else
		{
			PhysicsServer3D.ShapeSetData(Sphere, explosionRadius);
			SphereCast.CollisionMask = GameManager.TakeDamageMask;
			SphereCast.Motion = Vector3.Zero;
			SphereCast.Transform = new Transform3D(GlobalTransform.Basis, Collision);
			var hits = SpaceState.IntersectShape(SphereCast);
			var max = hits.Count;

			for (int i = 0; i < max; i++)
			{
				var hit = hits[i];

				CollisionObject3D collider = (CollisionObject3D)hit["collider"];

				if (collider is Damageable damageable)
				{
					Vector3 hPosition = collider.Position;
					Vector3 Distance = (hPosition - Collision);
					float lenght;
					Vector3 impulseDir = Distance.GetLenghtAndNormalize(out lenght);

					if (collider == owner) //BFG never does self damage
						continue;
					else
						damageable.Damage(GD.RandRange(damageMin, damageMax) * 100, damageType, owner);
				}
			}

			if (!string.IsNullOrEmpty(OnDeathSpawn))
			{
				Node3D DeathSpawn = (Node3D)ThingsManager.thingsPrefabs[OnDeathSpawn].Instantiate();
				GameManager.Instance.TemporaryObjectsHolder.AddChild(DeathSpawn);
				DeathSpawn.Position = Collision + d;
				DeathSpawn.SetForward(-d);
				DeathSpawn.Rotate(d, (float)GD.RandRange(0, Mathf.Pi * 2.0f));
				if (fx != null)
				{
					fx.Reparent(DeathSpawn);
					fx.enableLifeTime = true;
				}
			}

			//Check if collider can be marked
			if (CheckIfCanMark(SpaceState, Hit, Collision))
			{
				Node3D DecalMark = (Node3D)ThingsManager.thingsPrefabs[decalMark].Instantiate();
				GameManager.Instance.TemporaryObjectsHolder.AddChild(DecalMark);
				DecalMark.Position = Collision + (Normal * .03f);
				DecalMark.SetForward(-Normal);
				DecalMark.Rotate((DecalMark.UpVector()).Normalized(), -Mathf.Pi * .5f);
				DecalMark.Rotate(Normal, (float)GD.RandRange(0, Mathf.Pi * 2.0f));
				if (!string.IsNullOrEmpty(secondaryMark))
				{
					Node3D SecondMark = (Node3D)ThingsManager.thingsPrefabs[secondaryMark].Instantiate();
					GameManager.Instance.TemporaryObjectsHolder.AddChild(SecondMark);
					SecondMark.Position = Collision + (Normal * .05f);
					SecondMark.SetForward(-Normal);
					SecondMark.Rotate((SecondMark.UpVector()).Normalized(), -Mathf.Pi * .5f);
					SecondMark.Rotate(Normal, (float)GD.RandRange(0, Mathf.Pi * 2.0f));
				}
			}
			/*
			if (damageType == DamageType.BFGBall)
			{
				PlayerInfo playerInfo = owner.GetComponent<PlayerInfo>();
				if (playerInfo != null)
				{
					Camera rayCaster = playerInfo.playerCamera.ViewCamera;
					Quaternion Camerarotation;
					RaycastHit[] hitRays = new RaycastHit[3];
					Ray r;
					int numRay = 0;
					int index = 0;
					Vector3 dir = cTransform.forward;
					dir.y = 0;
					Camerarotation = rayCaster.transform.rotation;
					rayCaster.transform.rotation = Quaternion.LookRotation(dir);
					for (int k = 0; (k <= BFGTracers.samples) && (numRay <= 40); k++)
					{
						r = rayCaster.ViewportPointToRay(new Vector3(BFGTracers.hx[index], BFGTracers.hy[index], 0f));
						index++;
						if (index >= BFGTracers.pixels)
							index = 0;
						int max = Physics.RaycastNonAlloc(r, hitRays, 300, GameManager.TakeDamageMask, QueryTriggerInteraction.Ignore);
						if (max > hitRays.Length)
							max = hitRays.Length;
						for (int i = 0; i < max; i++)
						{
							GameObject hit = hitRays[i].collider.gameObject;
							Damageable d = hit.GetComponent<Damageable>();
							if (d != null)
							{
								if (hit == owner)
									continue;

								while ((d.Dead == false) && (numRay <= 40))
								{
									d.Damage(Random.Range(49, 88), DamageType.BFGBlast, owner);
									d.Impulse(r.direction, pushForce);
									numRay++;
								}
								GameObject go = PoolManager.GetObjectFromPool(SecondaryOnDeathSpawn.name);
								go.transform.position = hit.transform.position + hit.transform.up * 0.5f;
							}
						}
					}
					rayCaster.transform.rotation = Camerarotation;
				}
			}
			*/
			if (!string.IsNullOrEmpty(_onDeathSound))
				SoundManager.Create3DSound(Collision, SoundManager.LoadSound(_onDeathSound));
			QueueFree();
			return;
		}
		Position -= d * speed * deltaTime;

	}
	public bool CheckIfCanMark(PhysicsDirectSpaceState3D SpaceState, CollisionObject3D collider, Vector3 collision)
	{
		if (collider is Damageable)
			return false;

		//Check if mapcollider are noMarks
		if (MapLoader.noMarks.Contains(collider))
			return false;

		//Check if collision in inside a fog Area
		PointIntersect.Position = collision;

		var hits = SpaceState.IntersectPoint(PointIntersect);
		if (hits.Count == 0)
			return true;

		return false;
	}
}
