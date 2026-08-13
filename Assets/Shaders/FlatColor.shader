Shader "ToTheSummit/FlatColor"
{
    // TEŞHİS ÇİZİMİ İÇİN DÜZ RENK. Boru hattına bağlı değil: `Material.SetPass` ile
    // anlık çizimde kullanılabiliyor ve sahnenin ışığı, sisi, gölgesi işin içine
    // girmiyor.
    //
    // KÖŞE RENGİ OKUNMUYOR. Unity'nin `Hidden/Internal-Colored` gölgelendiricisi mesh'in
    // köşe rengiyle çarpıyor; üretilen modelde köşe rengi var ve föydeki bütün parçalar
    // alacalı çıkıyordu.
    //
    // GEÇİCİ. Parça föyüyle birlikte silinecek (bkz. `DECISIONS.md`).
    Properties
    {
        _Color ("Renk", Color) = (1,1,1,1)
        _ZTest ("Derinlik sınaması", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZWrite On
            ZTest [_ZTest]
            Cull Back

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct Attributes { float4 vertex : POSITION; };
            struct Varyings { float4 position : SV_POSITION; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
