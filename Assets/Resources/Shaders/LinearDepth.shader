// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/LinearDepth"
{
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

 
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			float4 _CameraDepthTexture_ST;
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _CameraDepthTexture);
				return o;
			}

			sampler2D _CameraDepthTexture;
			fixed4 frag (v2f i) : SV_Target
			{
				// Retuns normalized depth between 0 and 1
				// float d = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv));
				// return Linear01Depth(d);

				// Returns the actual depth, max clamping at camera's far clipping plane
				return LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv));
			}
			ENDCG
		}
	}
}
