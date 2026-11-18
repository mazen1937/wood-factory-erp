import { ArrayMaxSize, ArrayMinSize, ArrayUnique, IsArray, IsBoolean, IsDateString, IsIn, IsNumber, IsOptional, IsString, IsUUID, Matches, Max, MaxLength, Min, MinLength } from 'class-validator';

export class ExecutionCommandDto {
  @IsUUID() operationId!: string;
  @IsIn(['start','progress','hold','resume','issue','return','waste','labor','consumable','accept','packaging_cost','wage_agreement','wage_due','quality_check','work_cost','cost_mode']) action!: string;
  @IsOptional() @IsBoolean() costFocused?: boolean;
  @IsOptional() @IsIn(['cutting','ashareet','assembly','general_labor']) costStage?:string;
  @IsOptional() @IsUUID() workerId?:string;
  @IsOptional() @IsString() @MaxLength(160) machineName?:string;
  @IsOptional() @IsIn(['unit','minute','hour','daily','weekly','monthly']) costBasis?:string;
  @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) workDate!: string;
  @IsOptional() @IsString() @MaxLength(2000) notes?: string;
  @IsOptional() @IsArray() @ArrayMinSize(1) @ArrayMaxSize(30) @ArrayUnique() @IsString({each:true}) stages?: string[];
  @IsOptional() @IsUUID() stageId?: string;
  @IsOptional() @IsUUID() itemId?: string;
  @IsOptional() @IsUUID() warehouseId?: string;
  @IsOptional() @IsUUID() originalIssueId?: string;
  @IsOptional() @IsUUID() rateId?: string;
  @IsOptional() @IsUUID() wageAgreementId?: string;
  @IsOptional() @IsIn(['unit','daily','weekly','monthly']) wageBasis?: string;
  @IsOptional() @IsIn(['direct','pooled']) treatment?: string;
  @IsOptional() @IsNumber({maxDecimalPlaces:4}) @Min(0.0001) @Max(999999999) rate?: number;
  @IsOptional() @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) periodStart?: string;
  @IsOptional() @IsIn(['accepted','rejected']) decision?: string;
  @IsOptional() @IsBoolean() reinspection?: boolean;
  @IsOptional() @IsBoolean() workAlreadyCompleted?: boolean;
  @IsOptional() @IsString() @MaxLength(160) workerName?: string;
  @IsOptional() @IsString() @MaxLength(120) documentReference?: string;
  @IsOptional() @IsNumber({maxDecimalPlaces:4}) @Min(0.0001) @Max(999999999) quantity?: number;
  @IsOptional() @IsNumber({maxDecimalPlaces:4}) @Min(0.0001) @Max(999999999) amount?: number;
}

export class WorkerDto {
 @IsUUID() id!:string;
 @IsString() @MinLength(1) @MaxLength(60) code!:string;
 @IsString() @MinLength(1) @MaxLength(160) nameAr!:string;
 @IsOptional() @IsString() @MaxLength(160) nameEn?:string;
}

export class LaborRateDto {
  @IsString() @MinLength(1) @MaxLength(60) code!: string;
  @IsString() @MinLength(1) @MaxLength(160) nameAr!: string;
  @IsString() @MinLength(1) @MaxLength(60) stageCode!: string;
  @IsNumber({maxDecimalPlaces:4}) @Min(0) @Max(999999999) rate!: number;
  @IsIn(['direct','pooled']) treatment!: string;
  @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) effectiveFrom!: string;
  @IsOptional() @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) effectiveTo?: string;
}
