// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/UniformColor"
{
	Properties
	{
		_ObjectColor ("Object Color", Color) = (1,1,1,1)
		_ClusterColor ("Cluster Color", Color) = (0,1,0,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			fixed4 _ObjectColor;
			fixed4 _ClusterColor;
			int _Source;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				if (_Source == 0)
				{
					return _ObjectColor;	
				}
				else if (_Source == 1)
				{
					return _ClusterColor;
				}

				return 0;
			}
			ENDCG
		}
	}
}
