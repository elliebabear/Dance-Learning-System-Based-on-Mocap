// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Custom/Depth Billboard"
{
	Properties
	{
		_Size("Size", Range(0, 3)) = 0.03 //patch size
		_ColorTex("Texture", 2D) = "white" {}
		_DepthTex("TextureD", 2D) = "white" {}
		_BodyIndexTex("TextureB", 2D) = "white" {}


		_Dev("Dev", Range(-5, 5)) = 0
		_Gamma("Gamma", Range(0, 6)) = 2.41
		_SigmaX("SigmaX", Range(-3, 3)) = 0.10
		_SigmaY("SigmaY", Range(-3, 3)) = 0.10
		_Alpha("Alpha", Range(0,10)) = 0.015

		_SizeFilter("SizeFilter",Int) = 2

		_sigmaS("SigmaS",Range(0.1,20)) = 3
		_sigmaL("SigmaL",Range(0.1,20)) = 3

		[Toggle] _calculateNormals("Normals", Float) = 0
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
			struct GS_INPUT
			{
				float4	pos		: POSITION;
				float4 color	: COLOR;
				float4 n		: NORMAL;
			};

			struct FS_INPUT
			{
				float4	pos		: POSITION;
				float2  tex0	: TEXCOORD0;
				float4 color	: COLOR;
			};


			// **************************************************************
			// Vars															*
			// **************************************************************

			float _Size;
			sampler2D  _ColorTex;
			sampler2D _DepthTex;
			sampler2D _BodyIndexTex;
			float4 _Color;
			float _calculateNormals;
			int _SizeFilter;
			int _RemoveBackground;
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
			GS_INPUT VS_Main(appdata_full v)
			{
				GS_INPUT output = (GS_INPUT)0;

				float4 c = tex2Dlod(_ColorTex,float4(v.vertex.x,v.vertex.y,0,0));
				int dValue = textureToDepth(v.vertex.x,v.vertex.y);
				if (dValue==0)
				{
					output.color = float4(0, 0, 0, 0);
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
					output.n = estimateNormal(v.vertex.x,v.vertex.y);
				}
				else
				{
					output.n = float4(0,0,0,0);
				}
				return output;
			}

				float rand(float2 myVector) {
					return frac(sin(dot(myVector ,float2(12.9898,78.233))) * 43758.5453);
				}

				  // Geometry Shader -----------------------------------------------------
				  [maxvertexcount(4)]
				  void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
				  {
					  if (p[0].color.a == 0) return;

					  //float3 look = _WorldSpaceCameraPos - p[0].pos;	

					  //----------------------SPLAT ORIENTATION
					  float3 up = UNITY_MATRIX_IT_MV[1].xyz;
					  float3 right = UNITY_MATRIX_IT_MV[0].xyz;
					  if (_calculateNormals == 1) {
						  float nx = p[0].n.x;
						  float ny = p[0].n.y;
						  float nz = p[0].n.z;
						  float n = sqrt(pow(nx,2) + pow(ny,2) + pow(nz,2));

						  float h1 = max(nx - n , nx + n);
						  float h2 = ny;
						  float h3 = nz;
						  float h = sqrt(pow(h1,2) + pow(h2,2) + pow(h3,2));
						  right = float3(-2 * h1 * h2 / pow(h,2), 1 - 2 * pow(h2,2) / pow(h,2), -2 * h2 * h3 / pow(h,2));
						  up = float3(-2 * h1 * h3 / pow(h,2), -2 * h2 * h3 / pow(h,2), 1 - 2 * pow(h3,2) / pow(h,2));
					  }

					  //if(abs(look.y) > abs(look.x) || abs(look.y) > abs(look.z))
					  //	up = float3(1,0,0);

					  //look.y = 0;
					  //look = normalize(look);
					  up = normalize(up);
					  right = normalize(right);
					  //float3 right = cross(up, look);
					  //----------------------SPLAT ORIENTATION

					  //--------------------------RANDOMNESS
					  float theta1 = fmod(rand(float2(p[0].pos.x,p[0].pos.x)) * 1000,PI3) - PI6;
					  float theta2 = fmod(rand(float2(p[0].pos.y,p[0].pos.y)) * 1000,PI3) - PI6;
					  float theta3 = 0;

					  if (abs(theta2 - theta1) > PI6) {
						  theta2 = theta1 < 0 ? theta1 + PI6 : theta1 - PI6;
					  }
					  theta3 = fmod(rand(float2(p[0].pos.z,p[0].pos.z)) * 1000,PI3) - PI6;
					  if (abs(theta3 - theta2) > PI6) {
						  theta3 = theta2 < 0 ? theta2 + PI6 : theta2 - PI6;
					  }
					  //--------------------------RANDOMNESS


					  //--------------------------SPLAT SIZE
					  float size =  (p[0].pos.z * _Size) / camera_calibration[2];
					  //float size = 0.008;
					  float halfS = 0.5f * size;
					  //--------------------------SPLAT SIZE


					  //--------------------------CREATING QUADS
					  float4 v[4];
					  v[0] = float4(p[0].pos + halfS * right - halfS * up, 1.0f);
					  v[1] = float4(p[0].pos + halfS * right + halfS * up, 1.0f);
					  v[2] = float4(p[0].pos - halfS * right - halfS * up, 1.0f);
					  v[3] = float4(p[0].pos - halfS * right + halfS * up, 1.0f);

					  //float4 vp = UnityObjectToClipPos(unity_WorldToObject);
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

				  float gaussianTheta(float x, float x0, float y,float y0, float a, float sigmax, float sigmay, float gamma,float theta) {


					  float x2 = cos(theta) * (x - x0) - sin(theta) * (y - y0) + x0;
					  float y2 = sin(theta) * (x - x0) + cos(theta) * (y - y0) + y0;
					  float z2 = a * exp(-0.5 * (pow(pow((x2 - x0) / sigmax,2),gamma / 2))) * exp(-0.5 * (pow(pow((y2 - y0) / sigmay,2),gamma / 2)));
					  return z2;
				  }

				  // Fragment Shader -----------------------------------------------
				  float4 FS_Main(FS_INPUT input) : COLOR
				  {
					  float2 uv = input.tex0.xy;

					  //------brush stroke generation------//
					  // Single shape
					  //float alpha = gaussiantheta(uv.x, 0.5,uv.y,0.5,0.017,0.5,0.5,2,0);

					  float xc[9] = {0.25f,0.5f,0.75f,0.25f,0.5f,0.75f,0.25f,0.5f,0.75f};
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
					   alpha = a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8 + a9;

					   //----------------------------------//

					   //------------------------Multiply alpha mask by pixel value
					   alpha = alpha > 1 ? 1 : alpha;
					   alpha = alpha < 0.05 ? 0 : alpha;
					   float4 t = float4(1.0f,1.0f,1.0f,alpha);
					   float3 normal;
					   if (t.a == 0)
						   discard;
					   t = t * input.color;
					   //----------------------------------//

					   //-----------------------Saturation correction 
					   saturation = 1.2f;
					   float  p = sqrt(t.r * t.r * 0.299 + t.g * t.g * 0.587 + t.b * t.b * 0.114);
					   t.r = p + ((t.r) - p) * (saturation + 0.3);
					   t.g = p + ((t.g) - p) * (saturation + 0.3);
					   t.b = p + ((t.b) - p) * (saturation + 0.3);
					   return  t;
					   //----------------------------------//

				}


				  ENDCG
			  }
		}
}