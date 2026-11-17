import {
  BadRequestException,
  Body,
  Controller,
  Delete,
  Get,
  Headers,
  Param,
  ParseUUIDPipe,
  Post,
  Put,
  Res,
  UnauthorizedException,
  UploadedFile,
  UploadedFiles,
  UseInterceptors,
} from '@nestjs/common';
import { FileInterceptor, FilesInterceptor } from '@nestjs/platform-express';
import { plainToInstance } from 'class-transformer';
import { validate } from 'class-validator';
import { ConfirmOrderDto } from './confirm-order.dto';
import { IsArray, IsBoolean, IsDateString, IsIn, IsNotEmpty, IsNumber, IsOptional, IsString, IsUUID, Max, MaxLength, Min, MinLength, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { createReadStream } from 'node:fs';
import { AuthService } from '../auth/auth.service';
import { CreateEngineeringCutListPartInput, CreateEngineeringProductMaterialLineInput, OrdersService, UploadEngineeringFileMetadataInput, UploadedEngineeringFile } from './orders.service';

class CreateOrderDto {
  @IsString() @IsNotEmpty() @MaxLength(60) orderNo!: string;
  @IsUUID() customerId!: string;
  @IsDateString() orderDate!: string;
  @IsOptional() @IsDateString() requestedDeliveryDate?: string;
  @IsOptional() @IsString() @MaxLength(2000) notes?: string;
}

class CreateEngineeringPackageDto {
  @IsString() @IsNotEmpty() @MaxLength(80) productCode!: string;
  @IsString() @IsNotEmpty() @MaxLength(240) productDescription!: string;
  @IsBoolean() upholsteryRequired!: boolean;
  @IsOptional() @IsString() @MaxLength(4000) notes?: string;
}

class CreateEngineeringLineDto {
  @IsIn(['bom', 'cut_list']) lineType!: 'bom' | 'cut_list';
  @IsString() @IsNotEmpty() @MaxLength(240) description!: string;
  @IsOptional() @IsString() @MaxLength(120) materialReference?: string;
  @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999999) quantity!: number;
  @IsString() @IsNotEmpty() @MaxLength(40) unitCode!: string;
  @IsOptional() @IsString() @MaxLength(240) dimensions?: string;
  @IsOptional() @IsString() @MaxLength(1000) notes?: string;
}

class UploadEngineeringFileMetadataDto {
  @IsOptional() @IsIn(['product_reference', 'dimensioned_drawing', 'cut_list', 'bom_export', 'other']) attachmentType?: UploadEngineeringFileMetadataInput['attachmentType'];
  @IsOptional() @IsString() @MaxLength(240) description?: string;
  @IsOptional() @IsString() @MaxLength(80) materialCode?: string;
  @IsOptional() @IsString() @MaxLength(120) materialColor?: string;
  @IsOptional() @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0.01) @Max(99999999) thicknessMm?: number;
  @IsOptional() @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0.01) @Max(999999999) boardLengthMm?: number;
  @IsOptional() @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0.01) @Max(999999999) boardWidthMm?: number;
  @IsOptional() @IsUUID() materialItemId?: string;
  @IsOptional() @Type(() => Number) @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999) boardQuantity?: number;
  @IsOptional() @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0) @Max(100) wastePercentage?: number;
}

class CreateEngineeringProductMaterialLineDto implements CreateEngineeringProductMaterialLineInput {
  @IsIn(['accessory', 'raw_material', 'paint']) lineType!: 'accessory' | 'raw_material' | 'paint';
  @IsUUID() itemId!: string;
  @Type(() => Number) @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999) quantityPerUnit!: number;
  @IsOptional() @IsString() @MaxLength(1000) notes?: string;
}

class CreateEngineeringCutListPartDto implements CreateEngineeringCutListPartInput {
  @IsUUID() sourceFileId!: string;
  @IsString() @IsNotEmpty() @MaxLength(240) partName!: string;
  @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0.01) @Max(999999999) lengthMm!: number;
  @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0.01) @Max(999999999) widthMm!: number;
  @IsOptional() @Type(() => Number) @IsNumber({ maxDecimalPlaces: 2 }) @Min(0.01) @Max(99999999) thicknessMm?: number;
  @Type(() => Number) @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999) quantity!: number;
  @IsOptional() @IsString() @MaxLength(1000) notes?: string;
}

class CreateProductionOrderDto {
  @IsString() @IsNotEmpty() @MaxLength(60) orderNo!: string;
  @IsOptional() @IsUUID() engineeringPackageId?: string;
  @IsOptional() @IsString() @IsNotEmpty() @MaxLength(80) productCode?: string;
  @IsOptional() @IsString() @IsNotEmpty() @MaxLength(240) jobName?: string;
  @IsOptional() @IsDateString({strict:true}) plannedEndDate?: string;
  @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999999) plannedQuantity!: number;
  @IsOptional() @IsString() @MaxLength(240) productName?: string;
  @IsOptional() @IsDateString() productionDate?: string;
}

class ProductionWorkOrderLineDto {
  @IsOptional() @IsString() @MaxLength(80) catalogCode?: string | null;
  @IsString() @MaxLength(160) itemNameAr!: string;
  @IsString() @MaxLength(500) specification!: string;
  @IsString() @MaxLength(40) unitCode!: string;
  @IsOptional() @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) @Max(999999999999) quantity?: number | null;
  @IsBoolean() isSelected!: boolean;
  @IsBoolean() isCustom!: boolean;
}

class SaveProductionWorkOrderDto {
  @IsString() @IsNotEmpty() @MaxLength(240) productName!: string;
  @IsDateString() productionDate!: string;
  @IsArray() @ValidateNested({ each: true }) @Type(() => ProductionWorkOrderLineDto)
  lines!: ProductionWorkOrderLineDto[];
}

@Controller('orders')
export class OrdersController {
  constructor(private readonly auth: AuthService, private readonly service: OrdersService) {}

  private async user(authorization?: string): Promise<{ sub?: string; roles?: string[] }> {
    const token = authorization?.replace(/^Bearer\s+/i, '');
    if (!token) throw new UnauthorizedException();
    return await this.auth.me(token) as { sub?: string; roles?: string[] };
  }

  @Get()
  async listOrders(@Headers('authorization') authorization?: string) {
    await this.user(authorization);
    return this.service.listOrders();
  }

  @Post()
  async createOrder(@Headers('authorization') authorization: string, @Body() body: CreateOrderDto) {
    const user = await this.user(authorization);
    return this.service.createOrder(body, user.sub);
  }

  @Post('confirm')
  @UseInterceptors(FilesInterceptor('files', 100, { limits: { fileSize: 20 * 1024 * 1024, fieldSize: 1024 * 1024, fields: 1 } }))
  async confirmOrder(
    @Headers('authorization') authorization: string,
    @Body('payload') payload: string,
    @UploadedFiles() files: UploadedEngineeringFile[] = [],
  ) {
    const user = await this.user(authorization);
    let parsed: unknown;
    try { parsed = JSON.parse(payload); }
    catch { throw new BadRequestException('Invalid order payload.'); }
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new BadRequestException('Invalid order payload.');
    const body = plainToInstance(ConfirmOrderDto, parsed);
    const errors = await validate(body, { whitelist: true, forbidNonWhitelisted: true });
    if (errors.length) throw new BadRequestException(errors);
    return this.service.confirmOrder(body, files, user.sub);
  }

  @Get(':orderId/details')
  async orderDetails(@Headers('authorization') authorization: string, @Param('orderId', new ParseUUIDPipe()) orderId: string) {
    await this.user(authorization);
    return this.service.getOrderDetails(orderId);
  }

  @Get(':orderId/attachments/:fileId')
  async downloadOrderAttachment(
    @Headers('authorization') authorization: string,
    @Param('orderId', new ParseUUIDPipe()) orderId: string,
    @Param('fileId', new ParseUUIDPipe()) fileId: string,
    @Res() response: any,
  ) {
    await this.user(authorization);
    const file = await this.service.getOrderAttachment(orderId, fileId);
    const asciiName = file.file_name.replace(/[^\x20-\x7E]|["\\]/g, '_');
    response.setHeader('Content-Type', file.mime_type);
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Content-Disposition', `attachment; filename="${asciiName}"; filename*=UTF-8''${encodeURIComponent(file.file_name)}`);
    response.send(file.content);
  }

  @Get('production-orders')
  async listProductionOrders(@Headers('authorization') authorization?: string) {
    await this.user(authorization);
    return this.service.listProductionOrders();
  }

  @Get('production-orders/:productionOrderId/work-order')
  async getProductionWorkOrder(
    @Headers('authorization') authorization: string,
    @Param('productionOrderId', new ParseUUIDPipe()) productionOrderId: string,
  ) {
    await this.user(authorization);
    return this.service.getProductionWorkOrder(productionOrderId);
  }

  @Put('production-orders/:productionOrderId/work-order')
  async saveProductionWorkOrder(
    @Headers('authorization') authorization: string,
    @Param('productionOrderId', new ParseUUIDPipe()) productionOrderId: string,
    @Body() body: SaveProductionWorkOrderDto,
  ) {
    await this.user(authorization);
    return this.service.saveProductionWorkOrder(productionOrderId, body);
  }

  @Post(':orderId/production-orders')
  async createProductionOrder(
    @Headers('authorization') authorization: string,
    @Param('orderId', new ParseUUIDPipe()) orderId: string,
    @Body() body: CreateProductionOrderDto,
  ) {
    const user = await this.user(authorization);
    return this.service.createDraftProductionOrder(orderId, body, user.sub);
  }

  @Get(':orderId/engineering-packages')
  async listPackages(@Headers('authorization') authorization: string, @Param('orderId', new ParseUUIDPipe()) orderId: string) {
    await this.user(authorization);
    return this.service.listEngineeringPackages(orderId);
  }

  @Post(':orderId/engineering-packages')
  async createPackage(
    @Headers('authorization') authorization: string,
    @Param('orderId', new ParseUUIDPipe()) orderId: string,
    @Body() body: CreateEngineeringPackageDto,
  ) {
    const user = await this.user(authorization);
    return this.service.createEngineeringPackage(orderId, body, user.sub);
  }

  @Get('engineering-packages/:packageId/lines')
  async listLines(@Headers('authorization') authorization: string, @Param('packageId', new ParseUUIDPipe()) packageId: string) {
    await this.user(authorization);
    return this.service.listEngineeringLines(packageId);
  }

  @Post('engineering-packages/:packageId/lines')
  async addLine(
    @Headers('authorization') authorization: string,
    @Param('packageId', new ParseUUIDPipe()) packageId: string,
    @Body() body: CreateEngineeringLineDto,
  ) {
    await this.user(authorization);
    return this.service.addEngineeringLine(packageId, body);
  }

  @Get('engineering-packages/:packageId/product-material-lines')
  async listProductMaterialLines(@Headers('authorization') authorization: string, @Param('packageId', new ParseUUIDPipe()) packageId: string) {
    await this.user(authorization);
    return this.service.listEngineeringProductMaterialLines(packageId);
  }

  @Post('engineering-packages/:packageId/product-material-lines')
  async addProductMaterialLine(
    @Headers('authorization') authorization: string,
    @Param('packageId', new ParseUUIDPipe()) packageId: string,
    @Body() body: CreateEngineeringProductMaterialLineDto,
  ) {
    await this.user(authorization);
    return this.service.addEngineeringProductMaterialLine(packageId, body);
  }

  @Delete('engineering-packages/:packageId/product-material-lines/:lineId')
  async deleteProductMaterialLine(
    @Headers('authorization') authorization: string,
    @Param('packageId', new ParseUUIDPipe()) packageId: string,
    @Param('lineId', new ParseUUIDPipe()) lineId: string,
  ) {
    await this.user(authorization);
    return this.service.deleteEngineeringProductMaterialLine(packageId, lineId);
  }

  @Get('engineering-packages/:packageId/cut-list-parts')
  async listCutListParts(@Headers('authorization') authorization: string, @Param('packageId', new ParseUUIDPipe()) packageId: string) {
    await this.user(authorization);
    return this.service.listEngineeringCutListParts(packageId);
  }

  @Post('engineering-packages/:packageId/cut-list-parts')
  async addCutListPart(
    @Headers('authorization') authorization: string,
    @Param('packageId', new ParseUUIDPipe()) packageId: string,
    @Body() body: CreateEngineeringCutListPartDto,
  ) {
    await this.user(authorization);
    return this.service.addEngineeringCutListPart(packageId, body);
  }

  @Post('engineering-packages/:packageId/finalize')
  async finalizePackage(@Headers('authorization') authorization: string, @Param('packageId', new ParseUUIDPipe()) packageId: string) {
    await this.user(authorization);
    return this.service.finalizeEngineeringPackage(packageId);
  }

  @Get('engineering-packages/:packageId/files')
  async listFiles(@Headers('authorization') authorization: string, @Param('packageId', new ParseUUIDPipe()) packageId: string) {
    await this.user(authorization);
    return this.service.listPackageFiles(packageId);
  }

  @Post('engineering-packages/:packageId/files')
  @UseInterceptors(FileInterceptor('file', { limits: { fileSize: 20 * 1024 * 1024 } }))
  async uploadFile(
    @Headers('authorization') authorization: string,
    @Param('packageId', new ParseUUIDPipe()) packageId: string,
    @Body() metadata: UploadEngineeringFileMetadataDto,
    @UploadedFile() file?: UploadedEngineeringFile,
  ) {
    const user = await this.user(authorization);
    if (!file) throw new BadRequestException('اختر ملفاً قبل الرفع.');
    return this.service.uploadPackageFile(packageId, file, user.sub, metadata);
  }

  @Get('engineering-packages/:packageId/files/:fileId')
  async downloadFile(
    @Headers('authorization') authorization: string,
    @Param('packageId', new ParseUUIDPipe()) packageId: string,
    @Param('fileId', new ParseUUIDPipe()) fileId: string,
    @Res() response: any,
  ) {
    await this.user(authorization);
    const file = await this.service.getPackageFile(packageId, fileId);
    const asciiName = String(file.fileName).replace(/[^\x20-\x7E]/g, '_').replace(/["\\]/g, '_');
    response.setHeader('Content-Type', file.mimeType || 'application/octet-stream');
    response.setHeader('Content-Length', String(file.sizeBytes));
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('Content-Disposition', `attachment; filename="${asciiName}"; filename*=UTF-8''${encodeURIComponent(file.fileName)}`);
    createReadStream(file.filePath).pipe(response);
  }
}
