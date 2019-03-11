using UnityEngine;
using System.Collections;

using Simplex;
public class ParticleTexScript: MonoBehaviour {

    public ComputeShader shader;
    public int TexResolutionX = 2560;
    public int TexResolutionY = 1440;
    public int TerrainTexResolution = 8 + 1;
    public int NumParticles = 10;
    public int nFrameCount;
    Renderer rend;
    RenderTexture RenderTexA, RenderTexB;
    RenderTexture TerrainTex;
    RenderTexture TerrainNormalTex;
    Texture2D TerrainBuffer;

    bool bMouseDown = false;


    struct MyParticle
    {
        public Vector2 pos;
        public Vector2 vel;
        public Vector2 acc;
        public Vector2 noise;
    }

    int NumRepulsors;
    Vector4[] RepulsorPos;
    Vector4[] RepulsorVel;

    ComputeBuffer particleBuffer;

    // Use this for initialization
    void Start () {
        nFrameCount = 0;

        RenderTexA = new RenderTexture(TexResolutionX, TexResolutionY, 24);
        RenderTexA.enableRandomWrite = true;
        RenderTexA.Create();

        RenderTexB = new RenderTexture(TexResolutionX, TexResolutionY, 24);
        RenderTexB.enableRandomWrite = true;
        RenderTexB.Create();

        TerrainTex = new RenderTexture(TerrainTexResolution, TerrainTexResolution, 24,  RenderTextureFormat.ARGBFloat);
        TerrainTex.enableRandomWrite = true;
        TerrainTex.Create();

        TerrainNormalTex = new RenderTexture(TerrainTexResolution, TerrainTexResolution, 24,  RenderTextureFormat.ARGBFloat); 
        TerrainNormalTex.enableRandomWrite = true;
        TerrainNormalTex.Create();

        InitTerrainTex();

        // Round particles UP to nearest number
        if((NumParticles % 10) > 0)
        {
            NumParticles += 10 - (NumParticles % 10);
        }        

        particleBuffer = new ComputeBuffer(NumParticles, sizeof(float) * 8, ComputeBufferType.Default);

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        ResetComputeSim();
    }
   void InitTerrainTex() {

        int length = TerrainTex.width, width = TerrainTex.height;
        float scale = 0.0010f;
        int num_levels = 20;

        float[][,] noiseValues = new float[num_levels][,];
        for( int l=0;l<num_levels;l++){
            Simplex.Noise.Seed = (int)(Random.Range(0,1000000)); // Optional
            float[,] noise = Simplex.Noise.Calc2D(length, width, scale*(2.0f*(l+1)) ) ;
            noiseValues[l] = new float[noise.GetLength(0),noise.GetLength(1)];
            noiseValues[l] = noise;
        }

        float [,] tex_buf = new float[TerrainTex.width, TerrainTex.height];

        TerrainBuffer = new Texture2D(TerrainTex.width, TerrainTex.height );
        RenderTexture.active = TerrainTex;

         for( int x = 0; x< TerrainTex.width; x++ ) {
            for( int y = 0; y<TerrainTex.height; y++ ) {
                float sum = 0.0f;
                for( int l=0;l<num_levels;l++){
                    sum += noiseValues[l][x,y]/(256.0f*2.0f*(l+1));
                    //if( x==0 && y<2 )
                        //Debug.Log("l" + l + " " + noiseValues[l][x,y]/(2550.0f*2.0f*(l+1))); 
                }
                tex_buf[x,y] = sum; 
            }
        }
        NormalizePixels( tex_buf );

        for( int x = 0; x< TerrainTex.width; x++ ) {
            for( int y = 0; y<TerrainTex.height; y++ ) {
                TerrainBuffer.SetPixel( x, y, new Color(0,0,tex_buf[x,y]) );
            }
        }
        TerrainBuffer.Apply();
        RenderTexture.active = null;
        Graphics.Blit(TerrainBuffer, TerrainTex);
    }
    /* 
    void InitTerrainTex() {
        
        TerrainBuffer = new Texture2D(TerrainTex.width, TerrainTex.height );
        RenderTexture.active = TerrainTex;

        for( int x = 0; x< TerrainTex.width; x++ )
            for( int y = 0; y<TerrainTex.height; y++ )
                TerrainBuffer.SetPixel(x, y, new Color(1, 0, 0 ) ); 


        TerrainBuffer.SetPixel(0, 0, new Color(0, 0, Random.Range(0.0f,10.0f)) );
        TerrainBuffer.SetPixel(TerrainTex.width-1, TerrainTex.height-1, new Color(0, 0, Random.Range(0.0f,10.0f)) );
        TerrainBuffer.SetPixel(0, TerrainTex.height-1, new Color(0, 0, Random.Range(0.0f,10.0f)) );
        TerrainBuffer.SetPixel(TerrainTex.width-1, 0, new Color(0, 0, Random.Range(0.0f,10.0f)) );

        SetNoisePixelsRecurse( 0, TerrainTex.width-1, 0, TerrainTex.height-1, TerrainBuffer );

        NormalizePixels( TerrainBuffer );

        TerrainBuffer.Apply();
        RenderTexture.active = null;
        Graphics.Blit(TerrainBuffer, TerrainTex);
    }
    */
    // X000X000X width = 9, first = 0, last = 8, center = 4, next = (0,4),(4,8)
    // 0123456789
    // X000X width = 5, first = 0, last = 4, center = 2, next = (0,2),(2,4)
    //   X0X     width = 3, first = 2, last = 4, center = 3
    //     X0X              first = 4, last = 6, center = 5
    // X0X

    // 00x00
    // 00000
    // x000x
    // 00000
    // 00x00

    void NormalizePixels( float [,] tex_buf ) {
        float max_blue = -10000.0f, min_blue = 10000.0f;
        for( int x = 0; x< TerrainTex.width; x++ ) {
            for( int y = 0; y<TerrainTex.height; y++ ) {
                float blue = tex_buf[x,y];

                max_blue = Mathf.Max( blue, max_blue );
                min_blue = Mathf.Min( blue, min_blue );
            }
        }

        Debug.Log( " " + min_blue + " " + max_blue );

        for( int x = 0; x< TerrainTex.width; x++ ) {
            for( int y = 0; y<TerrainTex.height; y++ ) {
                float new_blue = tex_buf[x,y];

                    if( x==0 && y<2 )
                        Debug.Log(new_blue + " " + (new_blue - min_blue)/(max_blue-min_blue)); 
                tex_buf[x,y] = (new_blue - min_blue)/(max_blue-min_blue);
            }
        }
    }

    void SetNoisePixelsRecurse( int left, int right, int bottom, int top, Texture2D tex ) {
        int left_right_midpoint = left+(right-left)/2;
        int top_bottom_midpoint = bottom+(top-bottom)/2;

        float rand_pct = ((float)right-left)/tex.width;

        Color right_top = tex.GetPixel( right, top );
        Color right_bottom = tex.GetPixel( right, bottom );
        Color left_top = tex.GetPixel( left, top );
        Color left_bottom = tex.GetPixel( left, bottom );
        Color average;

        // top edge
        Color rand_color;

        if( tex.GetPixel( left_right_midpoint, top )[0] > 0.0f ) {
            rand_color = new Color(0, 0, Random.Range(rand_pct*-1.0f,rand_pct*1.0f));
            average = (left_top + right_top)/2.0f;
            tex.SetPixel( left_right_midpoint, top, average + rand_color );
        }

        // bottom edge
        if( tex.GetPixel( left_right_midpoint, bottom )[0] > 0.0f ) {
            rand_color = new Color(0, 0, Random.Range(rand_pct*-1.0f,rand_pct*1.0f));
            average = (left_bottom + right_bottom)/2.0f;
            tex.SetPixel( left_right_midpoint, bottom, average + rand_color );
        }

        // left edge
        if( tex.GetPixel( left, top_bottom_midpoint )[0] > 0.0f ) {
            rand_color = new Color(0, 0, Random.Range(rand_pct*-1.0f,rand_pct*1.0f));
            average = (left_top + left_bottom)/2.0f;
            tex.SetPixel( left, top_bottom_midpoint, average + rand_color );
        }

        //right edge
        if( tex.GetPixel( right, top_bottom_midpoint )[0] > 0.0f ) {
            rand_color = new Color(0, 0, Random.Range(rand_pct*-1.0f,rand_pct*1.0f));
            average = (right_top + right_bottom)/2.0f;
            tex.SetPixel( right, top_bottom_midpoint, average + rand_color );
        }

        //center
        if( tex.GetPixel( left_right_midpoint, top_bottom_midpoint )[0] > 0.0f ) {
            average = (right_top + right_bottom + left_top + left_bottom)/4.0f;
            tex.SetPixel( left_right_midpoint, top_bottom_midpoint, average + new Color(0, 0, Random.Range(0.0f,0.1f)) );
        }

        if( right-left_right_midpoint > 1 ) {
            SetNoisePixelsRecurse( left, left_right_midpoint,      bottom, top_bottom_midpoint,  tex );
            SetNoisePixelsRecurse( left_right_midpoint, right,     bottom, top_bottom_midpoint,  tex );
            SetNoisePixelsRecurse( left, left_right_midpoint,      top_bottom_midpoint, top,     tex );
            SetNoisePixelsRecurse( left_right_midpoint, right,     top_bottom_midpoint, top,     tex );
        }
    }

    void OnDestroy()
    { 
        RenderTexA.Release();
        RenderTexB.Release();
        TerrainTex.Release();
        particleBuffer.Release(); 
    } 

    private void ResetComputeSim()
    {

        InitTerrainTex();

        NumRepulsors = 4;
        RepulsorPos = new Vector4[NumRepulsors];
        RepulsorVel = new Vector4[NumRepulsors];
        
         for( int i=0; i<NumRepulsors; i++ ) { 
            RepulsorVel[i] = new Vector4( Random.Range(-1.0f,1.0f), Random.Range(-1.0f,1.0f), Random.Range(-1.0f,1.0f), Random.Range(-1.0f,1.0f) );
            RepulsorPos[i].x = Random.Range(TexResolutionX*0.45f, TexResolutionX*0.55f);
            RepulsorPos[i].y = TexResolutionY*(i+1)/5.0f ;
            RepulsorPos[i].z = -0.5f;
        }

        MyParticle[] pArray = new MyParticle[NumParticles];
        //particleBuffer.GetData(pArray);

        for (int i=0; i<NumParticles; i++)
        {
            MyParticle p = new MyParticle();
            p.pos = new Vector2(Random.Range(0.0f, TexResolutionX), Random.Range(0, TexResolutionY) );
            p.vel = new Vector2(0.0f, -10.0f);
            p.acc = new Vector2(0.0f, 0.0f);
            p.noise = new Vector2( Random.Range( -1.0f, 1.0f ),  Random.Range( -1.0f, 1.0f ) );
            pArray[i] = p;
        }

        particleBuffer.SetData(pArray);
        ComputeStepFrame();
    }


    private void ComputeStepFrame()
    {
        shader.SetVectorArray("RepulsorPos", RepulsorPos);
        shader.SetInt("TexSizeX", TexResolutionX);
        shader.SetInt("TexSizeY", TexResolutionY);
        shader.SetInt("TerrainTexSizeX", TerrainTexResolution);
        shader.SetInt("TerrainTexSizeY", TerrainTexResolution);
        shader.SetFloat("DeltaTime", Time.deltaTime);

        RenderTexture RenderTex;
        RenderTexture RenderTexLastFrame;
        if( ++nFrameCount %2 == 1 ) {
            RenderTex = RenderTexB;
            RenderTexLastFrame = RenderTexA;
        } else {
            RenderTex = RenderTexA;
            RenderTexLastFrame = RenderTexB;
        }

        int kernelHandle = shader.FindKernel("CSRenderWipe");
        shader.SetTexture(kernelHandle, "Terrain", TerrainTex);
        shader.SetTexture(kernelHandle, "TerrainNormals", TerrainNormalTex);
        shader.SetTexture(kernelHandle, "Result", RenderTex);
        shader.SetTexture(kernelHandle, "ResultLastFrame", RenderTexLastFrame);
        shader.Dispatch(kernelHandle, TexResolutionX / 8, TexResolutionY / 8, 1);

        kernelHandle = shader.FindKernel("CSMain");
        shader.SetTexture(kernelHandle, "Result", RenderTex);
        shader.SetTexture(kernelHandle, "ResultLastFrame", RenderTexLastFrame);
        shader.SetTexture(kernelHandle, "TerrainNormals", TerrainNormalTex);
        shader.SetTexture(kernelHandle, "Terrain", TerrainTex);
        shader.SetBuffer(kernelHandle, "PartBuffer", particleBuffer);
        shader.Dispatch(kernelHandle, NumParticles / 10, 1, 1);

        kernelHandle = shader.FindKernel("CSUpdateTerrainNormals");
        shader.SetTexture(kernelHandle, "Terrain", TerrainTex);
        shader.SetTexture(kernelHandle, "TerrainNormals", TerrainNormalTex);
        shader.Dispatch(kernelHandle, TerrainTexResolution / 8, TerrainTexResolution / 8, 1);

        rend.material.SetTexture("_MainTex", RenderTex); 
    }

    void Update () {

        for( int i=0; i<NumRepulsors; i++ ) {
            RepulsorPos[i].x += RepulsorVel[i].x;

            if ( RepulsorPos[i].x > TexResolutionX ) {
                RepulsorPos[i].x = 0;
            } else if( RepulsorPos[i].x < 0 ) {
                RepulsorPos[i].x = TexResolutionX;
            }
            RepulsorPos[i].z = 0.0f;
        }
        
        if( Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) )
        {
            bMouseDown = true;
        }

        if( Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) )
        {
            bMouseDown = false;
        }

/*
        RepelPoint = Vector4.zero;
        if(bMouseDown)
        {
            RaycastHit hit;
            Ray mr = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(mr, out hit))
            {
                RepelPoint.x = hit.textureCoord.x * TexResolutionX;
                RepelPoint.y = hit.textureCoord.y * TexResolutionY;
                 RepelPoint.z = 1.0f;
                if( Input.GetMouseButton(1) ) {
                    RepelPoint.z *= -1.0f;
                }
              RepelPoint.w = Input.GetMouseButton(0) ? +100.0f:-100.0f;
            }
            else
            {
                RepelPoint = Vector4.zero;
            }
        } */

        ComputeStepFrame();

        if (Input.GetKeyUp(KeyCode.E))
            ResetComputeSim();

        if (Input.GetKey("escape"))
            Application.Quit();
    }
}
