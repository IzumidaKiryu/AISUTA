#define LIL_URP

// [Yakiniku] Workaround for Android builds failing on Unity 6.3 (URP 17.3).
// URP's GlobalIllumination.hlsl reads the reflection probe keywords as values
// (e.g. if (_REFLECTION_PROBE_BOX_PROJECTION)) but only defines them as 0 when they are NOT declared.
// lilToon also runs this code in the vertex stage, where keywords declared with multi_compile_fragment
// are declared but undefined, which fails with 'undeclared identifier'. Define them as 0 when off
// (when on, Unity already defines them as 1). Re-apply this if lilToon is updated and overwrites the file.
#if defined(_REFLECTION_PROBE_BOX_PROJECTION_KEYWORD_DECLARED) && !defined(_REFLECTION_PROBE_BOX_PROJECTION)
    #define _REFLECTION_PROBE_BOX_PROJECTION 0
#endif
#if defined(_REFLECTION_PROBE_BLENDING_KEYWORD_DECLARED) && !defined(_REFLECTION_PROBE_BLENDING)
    #define _REFLECTION_PROBE_BLENDING 0
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"