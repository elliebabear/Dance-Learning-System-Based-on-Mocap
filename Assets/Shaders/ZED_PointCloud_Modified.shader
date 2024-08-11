//======= Copyright (c) Stereolabs Corporation, All rights reserved. ===============
//Displays point cloud though geometry
Shader "ZED/ZED PointCloudModified"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Size("Size", Range(0.1,2)) = 0.1
		_MaxDist("MaxDist", Range(0.01,20)) = 2

		_Dev("Dev", Range(-5, 5)) = 0
		_Gamma("Gamma", Range(0, 6)) = 2.41
		_SigmaX("SigmaX", Range(-3, 3)) = 0.10
		_SigmaY("SigmaY", Range(-3, 3)) = 0.10
		_Alpha("Alpha", Range(0,10)) = 0.015
	}
	SubShader
	{


		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag


			#include "UnityCG.cginc"
			
			#define PI3 1.04719755 
			#define PI6 0.523598776
			#define PI12 0.261799388
			#define EPS 1e-5



		struct FS_INPUT
		{
			float4	pos		: POSITION;
			float2  tex0	: TEXCOORD0;
			float4 color	: COLOR;
		};

		struct GS_INPUT
		{
			float4	pos		: POSITION;
			float4 color	: COLOR;
			float normal	: NORMAL;
		};

		

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			sampler2D _XYZTex;
			sampler2D _ColorTex;
			float4 _XYZTex_TexelSize;
			float4x4 _Position;

			float _MaxDist;
			float _Size;

			GS_INPUT vert (appdata_full v, uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
			{
				GS_INPUT o;
				o.normal = v.normal;

				//Compute the UVS
				float2 uv = float2(
							clamp(fmod(instance_id, _XYZTex_TexelSize.z) * _XYZTex_TexelSize.x, _XYZTex_TexelSize.x, 1.0 - _XYZTex_TexelSize.x),
							clamp(((instance_id -fmod(instance_id, _XYZTex_TexelSize.z) * _XYZTex_TexelSize.x) / _XYZTex_TexelSize.z) * _XYZTex_TexelSize.y, _XYZTex_TexelSize.y, 1.0 - _XYZTex_TexelSize.y)
							);

				


				//Load the texture
				float4 XYZPos = float4(tex2Dlod(_XYZTex, float4(uv, 0.0, 0.0)).rgb ,1.0f);

				//Set the World pos
				//o.pos = mul(mul(UNITY_MATRIX_VP, _Position ), XYZPos);
				o.pos = XYZPos;
				o.color =  float4(tex2Dlod(_ColorTex, float4(uv, 0.0, 0.0)).bgr ,1.0f);
				 
				if (XYZPos.z > _MaxDist)
					o.color.a = 0;

				return o;
			}

			 
			float rand(float2 myVector) {
				return frac(sin(dot(myVector, float2(12.9898, 78.233))) * 43758.5453);
			}

			[maxvertexcount(4)]
			void geom(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
			{
				if (p[0].color.a == 0) return;

				/*float3 up = float3(0, 1, 0);
				float3 look = _WorldSpaceCameraPos - p[0].pos;	
				look.y = 0;
				look = normalize(look);
				float3 right = cross(up, look);*/
				//----------------------SPLAT ORIENTATION
				float3 up = UNITY_MATRIX_IT_MV[1].xyz;
				float3 right = UNITY_MATRIX_IT_MV[0].xyz;
				/*float3 up = mul((float3x3)unity_CameraToWorld, float3(0, 1, 0));
				float3 right = mul((float3x3)unity_CameraToWorld, float3(1, 0, 0));*/

				up = normalize(up);
				right = normalize(right);
				float4 upColor = float4(abs(up.x), abs(up.y), abs(up.z), 1);

				//----------------------SPLAT ORIENTATION

				//--------------------------RANDOMNESS
			/*	float theta1 = fmod(rand(float2(p[0].pos.x, p[0].pos.x)) * 1000, PI3) - PI6;
				float theta2 = fmod(rand(float2(p[0].pos.y, p[0].pos.y)) * 1000, PI3) - PI6;
				float theta3 = 0;

				if (abs(theta2 - theta1) > PI6) {
					theta2 = theta1 < 0 ? theta1 + PI6 : theta1 - PI6;
				}
				theta3 = fmod(rand(float2(p[0].pos.z, p[0].pos.z)) * 1000, PI3) - PI6;
				if (abs(theta3 - theta2) > PI6) {
					theta3 = theta2 < 0 ? theta2 + PI6 : theta2 - PI6;
				}*/
				//--------------------------RANDOMNESS


				//--------------------------SPLAT SIZE
				float size = (p[0].pos.z * 0.006);
				//float size = 0.004;
				float halfS = 0.5f * size;
				//--------------------------SPLAT SIZE


				//--------------------------CREATING QUADS
				float4 v[4];

				float4 posWorld = mul(_Position, p[0].pos);
				/*v[0] = float4(p[0].pos + halfS * right - halfS * up, 1.0f);
				v[1] = float4(p[0].pos + halfS * right + halfS * up, 1.0f);
				v[2] = float4(p[0].pos - halfS * right - halfS * up, 1.0f);
				v[3] = float4(p[0].pos - halfS * right + halfS * up, 1.0f);*/

				v[0] = float4(posWorld + halfS * right - halfS * up, 1.0f);
				v[1] = float4(posWorld + halfS * right + halfS * up, 1.0f);
				v[2] = float4(posWorld - halfS * right - halfS * up, 1.0f);
				v[3] = float4(posWorld - halfS * right + halfS * up, 1.0f);

				//float4 vp = UnityObjectToClipPos(unity_WorldToObject);mul(mul(UNITY_MATRIX_VP, _Position), v[0]);
				FS_INPUT pIn;
				pIn.pos = UnityObjectToClipPos(v[0]);
				pIn.tex0 = float2(1.0f, 0.0f);
				pIn.color = p[0].color;
				triStream.Append(pIn);

				pIn.pos = UnityObjectToClipPos(v[1]);
				pIn.tex0 = float2(1.0f, 1.0f);
				pIn.color = p[0].color;
				triStream.Append(pIn);

				pIn.pos = UnityObjectToClipPos(v[2]);
				pIn.tex0 = float2(0.0f, 0.0f);
				pIn.color = p[0].color;
				triStream.Append(pIn);

				pIn.pos = UnityObjectToClipPos(v[3]);
				pIn.tex0 = float2(0.0f, 1.0f);
				pIn.color = p[0].color;
				triStream.Append(pIn);
				//--------------------------CREATING QUADS
			}


			float theta1;
			float theta2;
			float theta3;

			float alph;
			bool aBuffer = true;
			float saturation;

			float _Dev;
			float _Gamma;
			float _SigmaX;
			float _SigmaY;
			float _Alpha;

			float gaussianTheta(float x, float x0, float y, float y0, float a, float sigmax, float sigmay, float gamma, float theta) {

				float x2 = cos(theta) * (x - x0) - sin(theta) * (y - y0) + x0;
				float y2 = sin(theta) * (x - x0) + cos(theta) * (y - y0) + y0;
				float z2 = a * exp(-0.5 * (pow(pow((x2 - x0) / sigmax, 2), gamma / 2))) * exp(-0.5 * (pow(pow((y2 - y0) / sigmay, 2), gamma / 2)));
				return z2;
				//return 1;
			}

			float4 frag(FS_INPUT input) : COLOR
			{
				float2 uv = input.tex0.xy;

				////------brush stroke generation------//
				//// Single shape
				float alpha = gaussianTheta(uv.x, 0.5,uv.y,0.5,0.09,0.5,0.5,2,0);

				/*float xc[9] = {0.25f,0.5f,0.75f,0.25f,0.5f,0.75f,0.25f,0.5f,0.75f};
				float yc[9] = {0.25f,0.25f,0.25f,0.5f,0.5f,0.5f,0.75f,0.75f,0.75f};
				float alpha = 0;
				float a1 = gaussianTheta(uv.x, xc[0],uv.y,yc[0] + _Dev - theta1 / 6,_Alpha,_SigmaX,_SigmaY,_Gamma,theta1);
				float a2 = gaussianTheta(uv.x, xc[1],uv.y,yc[1] + _Dev - theta2 / 6,_Alpha * 0.9,_SigmaX,_SigmaY,_Gamma,theta2);
				float a3 = gaussianTheta(uv.x, xc[2],uv.y,yc[2] + _Dev - theta3 / 6,_Alpha * 0.9,_SigmaX,_SigmaY,_Gamma,theta3);
				float a4 = gaussianTheta(uv.x, xc[3],uv.y,yc[3] - theta1 / 6,_Alpha,_SigmaX,_SigmaY,_Gamma,theta1);
				float a5 = gaussianTheta(uv.x, xc[4],uv.y,yc[4] - theta2 / 6,_Alpha * 0.9,_SigmaX,_SigmaY,_Gamma,theta2);
				float a6 = gaussianTheta(uv.x, xc[5],uv.y,yc[5] - theta3 / 6,_Alpha * 0.9,_SigmaX,_SigmaY,_Gamma,theta3);
				float a7 = gaussianTheta(uv.x, xc[6],uv.y,yc[6] - _Dev - theta1 / 6,_Alpha,_SigmaX,_SigmaY,_Gamma,theta1);
				float a8 = gaussianTheta(uv.x, xc[7],uv.y,yc[7] - _Dev - theta2 / 6,_Alpha * 0.9,_SigmaX,_SigmaY,_Gamma,theta2);
				float a9 = gaussianTheta(uv.x, xc[8],uv.y,yc[8] - _Dev - theta3 / 6,_Alpha * 0.9,_SigmaX,_SigmaY,_Gamma,theta3);
				 alpha = a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8 + a9;*/

				// //----------------------------------//

				// //------------------------Multiply alpha mask by pixel value
				/* alpha = alpha > 1 ? 1 : alpha;*/
				 alpha = alpha < 0.05 ? 0 : alpha;
				 float4 t = float4(1.0f,1.0f,1.0f,alpha);
				 float3 normal;
				 if (t.a == 0)
					 discard;
				 t = t * input.color;
				// //----------------------------------//

				// //-----------------------Saturation correction 
				// saturation = 1.2f;
				// float  p = sqrt(t.r * t.r * 0.299 + t.g * t.g * 0.587 + t.b * t.b * 0.114);
				// t.r = p + ((t.r) - p) * (saturation + 0.3);
				// t.g = p + ((t.g) - p) * (saturation + 0.3);
				// t.b = p + ((t.b) - p) * (saturation + 0.3);
				 return   t;
				 //----------------------------------//

			}
			ENDCG
		}
	}
}
