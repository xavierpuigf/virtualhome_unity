// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/CompressedDepth"
{
	Properties
    {
        _CompressionFactor ("Compression Factor", Range(0.0, 1.0)) = 0.5
    }
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
			float _CompressionFactor;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _CameraDepthTexture);
				return o;
			}

			// Z buffer to linear 0..1 depth (0 at near plane, 1 at far plane)
			inline float Linear01DepthFromNear( float z )
			{
				// Based on http://www.humus.name/temp/Linearize%20depth.txt
				// LZ = z / (far - z * (far - near))

				// given
				// _ZBufferParams.x = (1-far/near)
				// _ZBufferParams.y = (far/near)
				return 1.0 / (_ZBufferParams.x + _ZBufferParams.y / z);

				/*
				// Derivation:
				float invNear = _ZBufferParams.w;
				float invFar = _ZBufferParams.z + invNear;
				float near = 1.0/invNear;
				float far = _ZBufferParams.y*near;
				float kk = near * invFar;
				float linearZFrom0 = Linear01Depth(d);
				linearZFrom0 = 1.0 / (_ZBufferParams.x * d + _ZBufferParams.y);
				float linearZFromNear = (linearZFrom0 - kk) / (1 - kk);
				linearZFromNear = (1.0/(_ZBufferParams.x * d + _ZBufferParams.y) - kk) 			/ (1 - kk); 
				linearZFromNear = (1.0/(_ZBufferParams.x * d + _ZBufferParams.y) - near*invFar) / (1 - near*invFar); 
				linearZFromNear = (1.0/((1-far*invNear) * d + far*invNear) - near*invFar)		/ (1 - near*invFar); 
				linearZFromNear = (1.0/(d - d*far*invNear + far*invNear) - near*invFar)			/ (1 - near*invFar); 
				linearZFromNear = (1.0/(d - d*far*invNear + far*invNear) - near*invFar)			/ (1 - near*invFar); 
				linearZFromNear = (d * (far-near) / (far*(d - d * far/near + far/near)))		/ (1 - near*invFar);
				linearZFromNear = d / (d - d * far/near + far/near);
				linearZFromNear = 1 / (1 - far/near + far/(near*d));
				*/
			}
			
			sampler2D _CameraDepthTexture;
			fixed4 frag (v2f i) : SV_Target
			{
				float d = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv));
				float linearZFromNear = Linear01DepthFromNear(d); // [0 @ near plane .. 1 @ far plane]
//				float k = 0.25; // compression factor
//				return pow(linearZFromNear, k);
				return pow(linearZFromNear, _CompressionFactor);
			}
			ENDCG
		}
	}
}
