import {
  Body,
  Controller,
  Get,
  Headers,
  Post,
  ForbiddenException,
  UnauthorizedException,
} from '@nestjs/common';
import { IsBoolean, IsIn, IsInt, IsNotEmpty, IsOptional, IsString, Max, MaxLength, Min } from 'class-validator';
import { AuthService } from '../auth/auth.service';
import { MasterDataService } from './master-data.service';

class UnitDto {
  @IsString() @IsNotEmpty() @MaxLength(40) code!: string;
  @IsString() @IsNotEmpty() @MaxLength(160) nameAr!: string;
  @IsString() @IsNotEmpty() @MaxLength(160) nameEn!: string;
  @IsInt() @Min(0) @Max(6) precisionDigits!: number;
}

class WarehouseDto {
  @IsString() @IsNotEmpty() @MaxLength(40) code!: string;
  @IsString() @IsNotEmpty() @MaxLength(160) nameAr!: string;
  @IsString() @IsNotEmpty() @MaxLength(160) nameEn!: string;
  @IsIn(['raw_material', 'finished_goods']) warehouseType!: string;
}

class PartyDto {
  @IsString() @IsNotEmpty() @MaxLength(40) code!: string;
  @IsIn(['customer', 'supplier', 'both']) partyType!: string;
  @IsString() @IsNotEmpty() @MaxLength(160) nameAr!: string;
  @IsOptional() @IsString() @MaxLength(160) nameEn?: string;
  @IsOptional() @IsString() @MaxLength(60) phone?: string;
}

class ItemDto {
  @IsString() @IsNotEmpty() @MaxLength(40) code!: string;
  @IsString() @IsNotEmpty() @MaxLength(160) nameAr!: string;
  @IsOptional() @IsString() @MaxLength(160) nameEn?: string;
  @IsIn(['raw_material', 'accessory', 'consumable', 'finished_good']) itemType!: string;
  @IsString() @IsNotEmpty() baseUnitId!: string;
  @IsBoolean() stockTracked!: boolean;
  @IsIn(['materials', 'industrial_expense']) classification!: string;
}

@Controller('master-data')
export class MasterDataController {
  constructor(private readonly auth: AuthService, private readonly service: MasterDataService) {}

  private async guard(authorization?: string, requireAdmin = false): Promise<void> {
    const token = authorization?.replace(/^Bearer\s+/i, '');
    if (!token) throw new UnauthorizedException();
    const session = await this.auth.me(token) as { roles?: string[] };
    if (requireAdmin && !session.roles?.includes('system_admin')) {
      throw new ForbiddenException('لا تملك صلاحية تعديل البيانات الأساسية.');
    }
  }

  @Get()
  async all(@Headers('authorization') authorization?: string) {
    await this.guard(authorization);
    return this.service.all();
  }

  @Post('units')
  async unit(@Headers('authorization') authorization: string, @Body() body: UnitDto) {
    await this.guard(authorization, true);
    return this.service.unit(body);
  }

  @Post('warehouses')
  async warehouse(@Headers('authorization') authorization: string, @Body() body: WarehouseDto) {
    await this.guard(authorization, true);
    return this.service.warehouse(body);
  }

  @Post('parties')
  async party(@Headers('authorization') authorization: string, @Body() body: PartyDto) {
    await this.guard(authorization, true);
    return this.service.party(body);
  }

  @Post('items')
  async item(@Headers('authorization') authorization: string, @Body() body: ItemDto) {
    await this.guard(authorization, true);
    return this.service.item(body);
  }
}
