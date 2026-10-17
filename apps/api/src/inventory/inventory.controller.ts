import { BadRequestException, Body, Controller, Get, Headers, Post, ForbiddenException, UnauthorizedException } from '@nestjs/common';
import { IsDateString, IsNumber, IsOptional, IsString, IsUUID, MaxLength, Min, MinLength } from 'class-validator';
import { AuthService } from '../auth/auth.service';
import { InventoryService } from './inventory.service';

class ReceiptDto {
  @IsUUID() operationId!: string;
  @IsUUID() itemId!: string;
  @IsUUID() warehouseId!: string;
  @IsOptional() @IsUUID() supplierId?: string;
  @IsString() @MinLength(1) @MaxLength(100) documentReference!: string;
  @IsDateString() movementDate!: string;
  @IsNumber({ maxDecimalPlaces: 4 }) @Min(0.0001) quantity!: number;
  @IsNumber({ maxDecimalPlaces: 4 }) @Min(0) unitCost!: number;
  @IsOptional() @IsString() @MaxLength(500) notes?: string;
}

@Controller('inventory')
export class InventoryController {
  constructor(private readonly auth: AuthService, private readonly service: InventoryService) {}

  private async user(authorization?: string, adminOnly = false): Promise<{ sub?: string; roles?: string[] }> {
    const token = authorization?.replace(/^Bearer\s+/i, '');
    if (!token) throw new UnauthorizedException();
    const session = await this.auth.me(token) as { sub?: string; roles?: string[] };
    if (adminOnly && !session.roles?.includes('system_admin')) throw new ForbiddenException('لا تملك صلاحية تسجيل حركة مخزون.');
    return session;
  }

  @Get('balances')
  async balances(@Headers('authorization') authorization?: string) {
    await this.user(authorization);
    return this.service.balances();
  }

  @Get('movements')
  async movements(@Headers('authorization') authorization?: string) {
    await this.user(authorization);
    return this.service.movements();
  }

  @Post('receipts')
  async receive(@Headers('authorization') authorization: string, @Body() body: ReceiptDto) {
    const session = await this.user(authorization, true);
    if (!Number.isFinite(body.quantity) || !Number.isFinite(body.unitCost)) throw new BadRequestException('الكمية والتكلفة يجب أن تكونا أرقاماً صحيحة.');
    return this.service.receive(body, session.sub);
  }
}
