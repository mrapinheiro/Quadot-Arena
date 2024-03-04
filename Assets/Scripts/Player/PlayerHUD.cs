using Godot;
using System;

public partial class PlayerHUD : MeshInstance3D
{
	[Export]
	public ShaderMaterial cameraUnderWaterMaterial;
	public void Init()
	{
		Mesh.SurfaceSetMaterial(0, cameraUnderWaterMaterial);
	}

	public void SetTexture(SubViewport viewport)
	{
		cameraUnderWaterMaterial.SetShaderParameter("screen_texture", viewport.GetTexture());
	}


	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}