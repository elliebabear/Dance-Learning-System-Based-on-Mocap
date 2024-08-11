// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Custom/Depth Mesh"
{
	Properties
	{
		_Size("Size", Range(0, 3)) = 0.03 //patch size
		_ColorTex("Texture", 2D) = "white" {}
		_DepthTex("TextureD", 2D) = "white" {}
		_BodyIndexTex("TextureB", 2D) = "white" {}

		_ShaderDistance("Mesh Edge distance", Range(0, 3)) = 0.03 
	}

		SubShader
		{
			Pass
			{
			Tags { "RenderType" = "Transparent" }

			Cull Off // render both back and front faces
			Blend Off

			CGPROGRAM

			#pragma target 5.0
			#pragma vertex VS_Main
			#pragma fragment FS_Main
			#pragma geometry GS_Main
			#include "UnityCG.cginc" 

			// **************************************************************
			// Data structures												*
			// **************************************************************
			struct v2f
				{
					float4	pos		: POSITION;
					float4 color	: COLOR;
					float4 normal	: NORMAL;
				};



			// **************************************************************
			// Vars															*
			// **************************************************************

			float _Size;
			sampler2D _ColorTex;
			sampler2D _DepthTex;
			sampler2D _BodyIndexTex;
			float4 _Color;
			float _calculateNormals;
			int _SizeFilter;
			int _RemoveBackground;
			bool _swapBR;
			float _sigmaL;
			float _sigmaS;
			float camera_calibration[14];
			float camera_width;
			float camera_height;
			#define PI3 1.04719755 
			#define PI6 0.523598776
			#define PI12 0.261799388
			#define EPS 1e-5

			// **************************************************************
			// Aux Functions												*
			// **************************************************************
			int textureToDepth(float x, float y)
			{
				float4 d = tex2Dlod(_DepthTex,float4(x, y,0,0));
				int dr = d.r * 255;
				int dg = d.g * 255;

				int dValue = dr | dg << 8;
				return dValue;
			}
			
			float2 transform_2d_point(float2 uv)
			{

				float cx = camera_calibration[0];
				float cy = camera_calibration[1];
				float fx = camera_calibration[2];
				float fy = camera_calibration[3];
				float k1 = camera_calibration[4];
				float k2 = camera_calibration[5];
				float k3 = camera_calibration[6];
				float k4 = camera_calibration[7];
				float k5 = camera_calibration[8];
				float k6 = camera_calibration[9];
				float codx = camera_calibration[10]; // center of distortion is set to 0 for Brown Conrady model
				float cody = camera_calibration[11];
				float p1 = camera_calibration[12];
				float p2 = camera_calibration[13];

				float2 xy;

				if (fx <= 0.f && fy <= 0.f)
				{
					//error, both must be positive
					return xy;
				}

				// correction for radial distortion
				float xp_d = (uv[0] - cx) / fx - codx;
				float yp_d = (uv[1] - cy) / fy - cody;

				float rs = xp_d * xp_d + yp_d * yp_d;
				float rss = rs * rs;
				float rsc = rss * rs;
				float a = 1.f + k1 * rs + k2 * rss + k3 * rsc;
				float b = 1.f + k4 * rs + k5 * rss + k6 * rsc;
				float ai;
				if (a != 0.f)
				{
					ai = 1.f / a;
				}
				else
				{
					ai = 1.f;
				}
				float di = ai * b;

				xy[0] = xp_d * di;
				xy[1] = yp_d * di;

				// approximate correction for tangential params
				float two_xy = 2.f * xy[0] * xy[1];
				float xx = xy[0] * xy[0];
				float yy = xy[1] * xy[1];

				xy[0] -= (yy + 3.f * xx) * p2 + two_xy * p1;
				xy[1] -= (xx + 3.f * yy) * p1 + two_xy * p2;

				// add on center of distortion
				xy[0] += codx;
				xy[1] += cody;

				//	return transformation_iterative_unproject(camera_calibration, uv, xy, valid, 20);
				return xy;
			}

			float bilateralFilterDepth(float depth, float x, float y)
			{
				if (_sigmaS == 0 || _sigmaL == 0) return depth;
				float sigS = max(_sigmaS, EPS);
				float sigL = max(_sigmaL, EPS);

				float facS = -1. / (2. * sigS * sigS);
				float facL = -1. / (2. * sigL * sigL);

				float sumW = 0.;
				float4  sumC = float4(0.0, 0.0, 0.0, 0.0);
				float halfSize = sigS * 2;
				float2 textureSize2 = float2(camera_width, camera_height);
				float2 texCoord = float2(x, y);
				float l = depth;

				for (float i = -halfSize; i <= halfSize; i++) {
					for (float j = -halfSize; j <= halfSize; j++) {
						float2 pos = float2(i, j);

						float2 coords = texCoord + pos / textureSize2;
						int offsetDepth = textureToDepth(coords.x, coords.y);
						float distS = length(pos);
						float distL = offsetDepth - l;

						float wS = exp(facS * float(distS * distS));
						float wL = exp(facL * float(distL * distL));
						float w = wS * wL;

						sumW += w;
						sumC += offsetDepth * w;
					}
				}
				return sumC / sumW;
			}

			float medianFilterDepth(int depth, float x, float y)
			{
				if (_SizeFilter == 0) return depth;
				float2 texCoord = float2(x, y);
				float2 textureSize2 = float2(camera_width, camera_height);
				int sizeArray = (_SizeFilter * 2 + 1) * (_SizeFilter * 2 + 1);

				int arr[121];

				int k = 0;
				for (float i = -_SizeFilter; i <= _SizeFilter; i++) {
					for (float j = -_SizeFilter; j <= _SizeFilter; j++) {
						float2 pos = float2(i, j);
						float2 coords = texCoord + pos / textureSize2;
						int d = textureToDepth(coords.x, coords.y);
						arr[k] = d;
						k++;
					}
				}

				for (int j = 1; j < sizeArray; ++j)
				{
					float key = arr[j];
					int i = j - 1;
					while (i >= 0 && arr[i] > key)
					{
						arr[i + 1] = arr[i];
						--i;
					}
					arr[i + 1] = key;
				}
				int index = (_SizeFilter * 2) + 1;
				return arr[index];
				//return depth;
			}

			float4 estimateNormal(float x, float y)
			{
				int width = camera_width;
				int height = camera_height;
				float yScale = 0.1;
				float xzScale = 1;
				float deltax = 1.0 / width;
				float deltay = 1.0 / height;
				float sx = textureToDepth(x < width - deltax ? x + deltax : x, y) - textureToDepth(x > 0 ? x - deltax : x, y);
				if (x == 0 || x == width - deltax)
					sx *= 2.5;

				float sy = textureToDepth(x, y < height - deltay ? y + deltay : y) - textureToDepth(x, y > 0 ? y - deltay : y);
				if (y == 0 || y == height - deltay)
					sy *= 2.5;

				float4 n = float4(-sx * yScale, sy * yScale, 2 * xzScale, 1);
				n = normalize(n);
				return n;
			}

			// **************************************************************
			// Shader Programs												*
			// **************************************************************

			// Vertex Shader ------------------------------------------------
			v2f VS_Main(appdata_full v)
			{
				v2f output = (v2f)0;

				float4 c = tex2Dlod(_ColorTex,float4(v.vertex.x,v.vertex.y,0,0));
				int dValue = textureToDepth(v.vertex.x,v.vertex.y);
				if (dValue==0)
				{
					output.color = float4(0, 0, 0, 1);
					return output;
				}

				float4 pos;

				//Median
				//dValue = medianFilterDepth(dValue,v.vertex.x,v.vertex.y);
				//Bilateral
				//float dValue2 = bilateralFilterDepth(dValue,v.vertex.x,v.vertex.y) ;
				
				float dValue2 = dValue / 1000.0;
				
				pos.z = dValue2;

				int x = camera_width * v.vertex.x;
				int y = camera_height * v.vertex.y;
				float vertx = float(x);
				float verty = float(camera_height-y);
				float2 xy = transform_2d_point(float2(vertx,verty));

				pos.x = xy.x*dValue2;
				pos.y = xy.y*dValue2;
				pos.w = 1;
				c.a = 1;
			
				output.pos = pos;
				output.color = c;

				if (_RemoveBackground) {
					float bi = tex2Dlod(_BodyIndexTex, float4(v.vertex.x, v.vertex.y, 0, 0)).a;
					if (bi == 1) {
						output.color = float4(0, 0, 0, 0);
						return output;
					}
				}

				if (_calculateNormals != 0)
				{
					output.normal = estimateNormal(v.vertex.x,v.vertex.y);
				}
				else
				{
					output.normal = float4(0,0,0,0);
				}

			
				return output;
			}

				float rand(float2 myVector) {
					return frac(sin(dot(myVector ,float2(12.9898,78.233))) * 43758.5453);
				}

				float _ShaderDistance;
				  // Geometry Shader -----------------------------------------------------
				  [maxvertexcount(3)]
				  void GS_Main(triangle v2f input[3], inout TriangleStream<v2f> OutputStream)
				  {
					  float lod = 0; // your lod level ranging from 0 to number of mipmap levels.
					  float c0 = input[0].color.a;
					  float c1 = input[1].color.a;
					  float c2 = input[2].color.a;

					  if (distance(input[0].pos, input[1].pos) < _ShaderDistance & distance(input[0].pos, input[2].pos) < _ShaderDistance & distance(input[1].pos, input[2].pos) < _ShaderDistance
						  & c0 != 0 & c1 != 0 & c2 != 0)
					  {
						  v2f outV;
						  outV.pos = UnityObjectToClipPos(input[0].pos);
						  outV.color = input[0].color;
						  outV.normal = input[0].normal;
						  OutputStream.Append(outV);
						  outV.pos = UnityObjectToClipPos(input[1].pos);
						  outV.color = input[1].color;
						  outV.normal = input[1].normal;
						  OutputStream.Append(outV);
						  outV.pos = UnityObjectToClipPos(input[2].pos);
						  outV.color = input[2].color;
						  outV.normal = input[2].normal;
						  OutputStream.Append(outV);
					  }
				  }

				  // Fragment Shader -----------------------------------------------
				  float4 FS_Main(v2f input) : COLOR
				  {
						  // sample the texture
					 fixed4 col = input.color;
					  // apply fog
					  UNITY_APPLY_FOG(input.fogCoord, col);
					  return col;
					}


				  ENDCG
			  }
		}
}