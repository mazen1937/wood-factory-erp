import { Type } from 'class-transformer';
import { ArrayMaxSize, IsArray, IsDateString, IsIn, IsInt, IsNotEmpty, IsNumber, IsOptional, IsString, IsUUID, Max, MaxLength, Min, ValidateNested } from 'class-validator';

class OrderAccessoryDto {
  @IsUUID() itemId!: string;
  @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999) quantity!: number;
}

class OrderAttachmentDto {
  @IsIn(['cut_list', 'product_reference', 'other']) attachmentType!: string;
  @IsString() @IsNotEmpty() @MaxLength(240) name!: string;
  @IsOptional() @IsString() @MaxLength(2000) description?: string;
  @IsOptional() @IsString() @MaxLength(120) color?: string;
}

export class ConfirmOrderDto {
  @IsString() @IsNotEmpty() @MaxLength(60) orderNo!: string;
  @IsOptional() @IsString() @IsNotEmpty() @MaxLength(240) orderName?: string;
  @IsUUID() customerId!: string;
  @IsDateString({ strict: true }) orderDate!: string;
  @IsOptional() @IsDateString({ strict: true }) requestedDeliveryDate?: string;
  @IsString() @IsNotEmpty() @MaxLength(80) productCode!: string;
  @IsString() @IsNotEmpty() @MaxLength(240) productName!: string;
  @IsOptional() @IsInt() @Min(0) @Max(999999999) totalBoards?: number;
  @IsOptional() @IsArray() @ArrayMaxSize(500) @ValidateNested({ each: true }) @Type(() => OrderAccessoryDto)
  materials?: OrderAccessoryDto[];
  @IsNumber({ maxDecimalPlaces: 2 }) @Min(0) @Max(100) wastePercentage!: number;
  @IsOptional() @IsString() @MaxLength(2000) notes?: string;
  @IsArray() @ArrayMaxSize(500) @ValidateNested({ each: true }) @Type(() => OrderAccessoryDto)
  accessories!: OrderAccessoryDto[];
  @IsArray() @ArrayMaxSize(100) @ValidateNested({ each: true }) @Type(() => OrderAttachmentDto)
  attachments!: OrderAttachmentDto[];
}
