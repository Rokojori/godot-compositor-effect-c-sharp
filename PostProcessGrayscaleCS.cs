
using System;
using Godot;
using System.Collections.Generic;

[Tool]
[GlobalClass]
public partial class PostProcessGrayscaleCS : CompositorEffect
{
	 
	public RenderingDevice rd;
	public Rid shader;
	public Rid pipeline;
    
  public PostProcessGrayscaleCS():base()
  {
    _Init();
  }

	public void _Init()
	{  
		EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
		rd = RenderingServer.GetRenderingDevice();    
		RenderingServer.CallOnRenderThread( Callable.From(  _InitializeCompute ) );
	
	}
	
  // System notifications, we want to react on the notification that
	// alerts us we are about to be destroyed.
	public override void _Notification( int what )
	{  
		if ( what == NotificationPredelete )
		{
			if ( shader.IsValid )
			{
				// Freeing our shader will also free any dependents such as the pipeline!
				rd.FreeRid(shader);
			}
		}
	}

	//Code in this region runs on the rendering thread.
	//Compile our shader at initialization.  
	public void _InitializeCompute()
	{  
		rd = RenderingServer.GetRenderingDevice();

		if ( rd == null )
		{
			return;
		}

    // Compile our shader.
		var shaderFile  = GD.Load<RDShaderFile>("res://post_process_grayscale.glsl");
		var shaderSpirv = shaderFile.GetSpirV();
	
		shader = rd.ShaderCreateFromSpirV( shaderSpirv );

		if ( shader.IsValid )
		{
			pipeline = rd.ComputePipelineCreate(shader);	
		}
	} 
  
  // Called by the rendering thread every frame.
	public override void _RenderCallback( int callbacktype, RenderData pRenderData )
	{  
    var callbackTypeEnum = (EffectCallbackTypeEnum) callbacktype;

		if ( rd != null  && callbackTypeEnum == EffectCallbackTypeEnum.PostTransparent && pipeline.IsValid )
		{
			// Get our render scene buffers object, this gives us access to our render buffers.
			// Note that implementation differs per renderer hence the need for the cast.
			var renderSceneBuffers  = ( RenderSceneBuffersRD ) pRenderData.GetRenderSceneBuffers();
      
			if ( renderSceneBuffers != null )
			{
				// Get our render size, this is the 3D render resolution!
				Vector2I size = renderSceneBuffers.GetInternalSize();

				if ( size.X == 0 && size.Y == 0)
				{
					return;
				}

        // We can use a compute shader here.

				var xGroups  = (size.X - 1) / 8 + 1;
				var yGroups  = (size.Y - 1) / 8 + 1;
				int zGroups  = 1;
	
        var pushConstant = new float[]{
          size.X,
					size.Y,
					0.0f,
					0.0f
        };

        var bytesList = new List<byte>();
        Array.ForEach( pushConstant, c => bytesList.AddRange( BitConverter.GetBytes( c ) ) );
        var pushConstantBytes = bytesList.ToArray();      

				int viewCount = (int) renderSceneBuffers.GetViewCount();

				for ( var i = 0; i < viewCount; i++)
				{
          var view = (uint) i;
					// Get the RID for our color image, we will be reading from && writing to it.
					Rid inputImage = renderSceneBuffers.GetColorLayer(view);
	
					// Create a uniform set, this will be cached, the cache will be cleared if our viewports configuration is changed.
					var uniform  = new RDUniform();
					uniform.UniformType = RenderingDevice.UniformType.Image;
					uniform.Binding = 0;
					uniform.AddId( inputImage );
					var uniformSet  = UniformSetCacheRD.GetCache(shader, 0, new Godot.Collections.Array<RDUniform>(){uniform});
	
					// Run our compute shader.
					var computeList  = rd.ComputeListBegin();
					rd.ComputeListBindComputePipeline(computeList, pipeline);
					rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
					rd.ComputeListSetPushConstant(computeList, pushConstantBytes, (uint) pushConstantBytes.Length );
					rd.ComputeListDispatch(computeList, (uint)xGroups, (uint)yGroups, (uint)zGroups);
					rd.ComputeListEnd();
	
				}
			}
		}
	}
	
	
	
}