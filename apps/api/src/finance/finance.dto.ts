import {Type} from 'class-transformer';
import {ArrayMaxSize,ArrayMinSize,ArrayUnique,IsArray,IsBoolean,IsDateString,IsIn,IsOptional,IsString,IsUUID,Matches,MaxLength,ValidateNested} from 'class-validator';
export class FinanceLineDto {
 @IsUUID() itemId!:string;
 @IsUUID() warehouseId!:string;
 @Matches(/^\d{1,12}(\.\d{1,4})?$/) quantity!:string;
 @IsOptional() @Matches(/^\d{1,12}(\.\d{1,4})?$/) unitPrice?:string;
 @IsOptional() @Matches(/^\d{1,12}(\.\d{1,4})?$/) discount?:string;
 @IsOptional() @IsUUID() taxPolicyId?:string;
 @IsOptional() @IsUUID() sourceLineId?:string;
}
export class FinanceAllocationDto {
 @IsUUID() obligationId!:string;
 @IsUUID() settlementId!:string;
 @Matches(/^\d{1,12}(\.\d{1,4})?$/) amount!:string;
}
export class FinanceCommandDto {
 @IsUUID() operationId!:string;
 @IsIn(['sale','sales_return','invoice','delivery','customer_receipt','supplier_payment','expense','supplier_invoice','purchase_fg','wage_payable','allocation','transfer','opening_treasury','customer_refund','reversal','production_completion','sale_and_delivery']) kind!:string;
 @IsOptional() @IsUUID() productionOrderId?:string;
 @IsOptional() @IsBoolean() costApproved?:boolean;
 @IsOptional() @IsBoolean() deliverNow?:boolean;
 @IsString() @MaxLength(120) @Matches(/\S/) reference!:string;
 @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) date!:string;
 @IsOptional() @IsUUID() partyId?:string;
 @IsOptional() @IsUUID() orderId?:string;
 @IsOptional() @IsUUID() sourceId?:string;
 @IsOptional() @IsUUID() obligationId?:string;
 @IsOptional() @IsUUID() accountId?:string;
 @IsOptional() @IsUUID() targetAccountId?:string;
 @IsOptional() @Matches(/^\d{1,12}(\.\d{1,4})?$/) amount?:string;
 @IsOptional() @Matches(/^\d{1,12}(\.\d{1,4})?$/) deliveryCharge?:string;
 @IsOptional() @IsIn(['industrial','administrative','customer_delivery']) category?:string;
 @IsOptional() @IsString() @MaxLength(2000) notes?:string;
 @IsOptional() @IsIn(['return','cancel']) returnType?:string;
 @IsOptional() @IsArray() @ArrayMinSize(1) @ArrayMaxSize(100) @ValidateNested({each:true}) @Type(()=>FinanceLineDto) lines?:FinanceLineDto[];
 @IsOptional() @IsArray() @ArrayMaxSize(100) @ValidateNested({each:true}) @Type(()=>FinanceAllocationDto) allocations?:FinanceAllocationDto[];
 @IsOptional() @IsArray() @ArrayMinSize(1) @ArrayMaxSize(100) @ArrayUnique() @IsUUID(undefined,{each:true}) saleIds?:string[];
 @IsOptional() @IsArray() @ArrayMinSize(1) @ArrayMaxSize(100) @ArrayUnique() @IsUUID(undefined,{each:true}) sourceIds?:string[];
 @IsOptional() @IsIn(['inventory_receipt','wage_accrual','legacy_labor','job_work']) sourceType?:string;
}
export class SellingPriceDto {
 @IsUUID() operationId!:string;
 @IsUUID() itemId!:string;
 @IsOptional() @IsUUID() orderId?:string;
 @Matches(/^\d{1,12}(\.\d{1,4})?$/) unitPrice!:string;
 @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) date!:string;
 @IsOptional() @IsString() @MaxLength(2000) notes?:string;
}
export class FinanceConfigDto {
 @IsUUID() operationId!:string;
 @IsIn(['account','tax']) kind!:string;
 @IsString() @MaxLength(60) @Matches(/\S/) code!:string;
 @IsString() @MaxLength(2000) @Matches(/\S/) name!:string;
 @IsOptional() @IsIn(['cash','bank']) accountType?:string;
 @IsOptional() @Matches(/^\d{1,3}(\.\d{1,4})?$/) rate?:string;
 @IsOptional() @Matches(/^\d{4}-\d{2}-\d{2}$/) @IsDateString({strict:true}) effectiveFrom?:string;
}
