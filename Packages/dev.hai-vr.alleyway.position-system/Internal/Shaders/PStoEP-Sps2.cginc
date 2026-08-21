#ifndef PSTOEP_SPS2_INCLUDED
#define PSTOEP_SPS2_INCLUDED

#include "PStoEP-Light.cginc"
#include "Packages/com.vrcfury.vrcfury/SPS/common/sps_cell_layout.cginc"
#include "Packages/com.vrcfury.vrcfury/SPS/common/sps_types.cginc"
SPS_INIT_TEX(_VFGridFinal)

struct PStoEPSps2Candidate
{
    PStoEPEntity entity;
    float distanceSq;
    uint ownerIdentity;
    uint entityIdentity;
};

PStoEPSps2Candidate PStoEP_InvalidSps2Candidate()
{
    PStoEPSps2Candidate candidate;
    candidate.entity = PStoEP_InvalidEntity();
    candidate.distanceSq = 3.402823466e+38;
    candidate.ownerIdentity = 0xffffffffu;
    candidate.entityIdentity = 0xffffffffu;
    return candidate;
}

uint PStoEP_Sps2SocketKind(uint flags)
{
    bool isHole = (flags & SPS_SOCKET_FLAG_HOLE) != 0u;
    bool isDoubleSided = (flags & SPS_SOCKET_FLAG_DOUBLE_SIDED) != 0u;
    if (isHole && isDoubleSided) return 0u;
    if (isHole) return PSTOEP_ENTITY_HOLE;
    if (isDoubleSided) return PSTOEP_ENTITY_RING;
    return PSTOEP_ENTITY_ONE_WAY_RING;
}

bool PStoEP_Sps2CandidateIsBetter(float distanceSq, uint ownerIdentity, uint entityIdentity, PStoEPSps2Candidate best)
{
    if (distanceSq < best.distanceSq) return true;
    if (distanceSq > best.distanceSq) return false;
    if (ownerIdentity < best.ownerIdentity) return true;
    if (ownerIdentity > best.ownerIdentity) return false;
    return entityIdentity < best.entityIdentity;
}

PStoEPEntity PStoEP_Sps2EntityFromCell(SpsCell cell, uint entityKind)
{
    return PStoEP_MakeFrameEntity(
        PStoEP_Descriptor(PSTOEP_SOURCE_SPS2_ATLAS, entityKind),
        sps_cell_header_player_id(cell),
        sps_cell_header_unique_id(cell),
        mul(unity_WorldToObject, float4(sps_cell_header_world(cell), 1.0)).xyz,
        mul((float3x3)unity_WorldToObject, sps_cell_header_forward(cell)),
        mul((float3x3)unity_WorldToObject, sps_cell_header_up(cell)),
        sps_cell_header_scale(cell));
}

void PStoEP_ConsiderSps2Cell(SpsCell cell, uint product, float3 observerWorld, inout PStoEPSps2Candidate best)
{
    if (!sps_cell_check_magic(cell)) return;
    if (cell.read_uint(SPS_HEADER_VENDOR_INDEX) != SPS_VENDOR_SPS) return;
    if (cell.read_uint(SPS_HEADER_PRODUCT_INDEX) != product) return;
    if (cell.read_uint(SPS_HEADER_VERSION_INDEX) != SPS_VERSION_SPS) return;

    uint entityKind = PSTOEP_ENTITY_PLUG;
    if (product == SPS_PRODUCT_SOCKET)
    {
        uint flags = cell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS));
        entityKind = PStoEP_Sps2SocketKind(flags);
        if (entityKind == 0u) return;
    }

    float3 worldPosition = sps_cell_header_world(cell);
    if (!PStoEP_IsFinite3(worldPosition)) return;
    float3 offset = worldPosition - observerWorld;
    float distanceSq = dot(offset, offset);
    uint ownerIdentity = sps_cell_header_player_id(cell);
    uint entityIdentity = sps_cell_header_unique_id(cell);
    if (!PStoEP_Sps2CandidateIsBetter(distanceSq, ownerIdentity, entityIdentity, best)) return;

    PStoEPEntity entity = PStoEP_Sps2EntityFromCell(cell, entityKind);
    if ((entity.fields & PSTOEP_FIELD_PRESENT) == 0u) return;
    best.entity = entity;
    best.distanceSq = distanceSq;
    best.ownerIdentity = ownerIdentity;
    best.entityIdentity = entityIdentity;
}

PStoEPSps2Candidate PStoEP_FindNearestSps2(uint product, float3 observerWorld)
{
    PStoEPSps2Candidate best = PStoEP_InvalidSps2Candidate();
    SpsTexture tex = SPS_GET_TEX(_VFGridFinal);
    uint slotCount = sps_socket_slot_count();
    uint groupCount = min((uint)SPS_CELL_DICTIONARY_GROUP_COUNT,
        (slotCount + (uint)SPS_CELL_DICTIONARY_GROUP_SIZE - 1u) / (uint)SPS_CELL_DICTIONARY_GROUP_SIZE);
    SpsCell dictionary = sps_get_slot_dictionary(tex);

    [loop]
    for (uint group = 0u; group < groupCount; group++)
    {
        if (!sps_cell_dictionary_group_used(dictionary, group)) continue;
        uint startIndex = group * (uint)SPS_CELL_DICTIONARY_GROUP_SIZE;
        [loop]
        for (uint member = 0u; member < (uint)SPS_CELL_DICTIONARY_GROUP_SIZE; member++)
        {
            uint cellIndex = startIndex + member;
            if (cellIndex >= slotCount) break;
            PStoEP_ConsiderSps2Cell(sps_get_cell(tex, (int)cellIndex), product, observerWorld, best);
        }
    }
    return best;
}

PStoEPProviderContext PStoEP_Sps2ProviderContext()
{
    PStoEPProviderContext context;
    float3 observerWorld = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    PStoEPSps2Candidate socket = PStoEP_FindNearestSps2(SPS_PRODUCT_SOCKET, observerWorld);
    bool hasSps2Socket = (socket.entity.fields & PSTOEP_FIELD_PRESENT) != 0u;
    // Compatibility lights are the legacy representation of an SPS2 socket.
    // Keep them as fallback when no atlas socket is available, but exclude all
    // of them once an authoritative atlas socket can represent the same target.
    context.entity0 = PStoEP_FindNearestClassicSocket(observerWorld, hasSps2Socket);
    if (hasSps2Socket)
    {
        float classicDistanceSq = 3.402823466e+38;
        if ((context.entity0.fields & PSTOEP_FIELD_PRESENT) != 0u)
        {
            float3 classicWorld = mul(unity_ObjectToWorld, float4(asfloat(context.entity0.position), 1.0)).xyz;
            float3 classicOffset = classicWorld - observerWorld;
            classicDistanceSq = dot(classicOffset, classicOffset);
        }
        if (socket.distanceSq <= classicDistanceSq) context.entity0 = socket.entity;
    }
    context.entity1 = PStoEP_FindNearestSps2(SPS_PRODUCT_PLUG, observerWorld).entity;
    return context;
}

#undef PSTOEP_PROVIDER_VERTEX_PREPARE
#define PSTOEP_PROVIDER_VERTEX_PREPARE(output) \
    PStoEPProviderContext pstoepVertexContext = PStoEP_Sps2ProviderContext(); \
    PSTOEP_WRITE_ENTITY(output, pstoepEntity0, pstoepVertexContext.entity0) \
    PSTOEP_WRITE_ENTITY(output, pstoepEntity1, pstoepVertexContext.entity1)

#endif
