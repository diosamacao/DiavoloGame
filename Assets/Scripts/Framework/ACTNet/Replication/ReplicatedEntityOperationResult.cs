/// <summary>描述实体注册表对生命周期操作的明确接受或拒绝原因。</summary>
public enum ReplicatedEntityOperationResult
{
    Success = 0,
    InvalidEntityId = 1,
    InvalidArchetypeId = 2,
    InvalidSchemaId = 3,
    DuplicateSpawn = 4,
    UnknownEntity = 5,
    SchemaMismatch = 6,
}
