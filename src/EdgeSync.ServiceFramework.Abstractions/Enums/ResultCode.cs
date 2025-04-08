namespace EdgeSync.ServiceFramework.Enums;

public enum ResultCode
{
    DTDLModelResultCodeOK = 60000,
    DTDLModelResultCodeInvalidFormat = 60001,
    DTDLModelResultCodeInvalidSchema = 60002,
    DTDLModelResultCodeInvalidSchemaType = 60003,
    DTDLModelResultCodeInvalidSchemaProperty = 60004,
    DTDLModelResultCodeInvalidSchemaPropertyType = 60005,
    DTDLModelResultCodeInternalError = 60006,

    ServiceResultInvalidData = 60007,
    ServiceResultInvalidDataFormat = 60008,
    ServiceResultInvalidDataSchema = 60009,
    ServiceResultInvalidSubject = 60010,

}