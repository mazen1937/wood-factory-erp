import { Module } from '@nestjs/common';
import { AuthController } from './auth/auth.controller';
import { AuthService } from './auth/auth.service';
import { HealthController } from './health.controller';
import { InventoryController } from './inventory/inventory.controller';
import { InventoryService } from './inventory/inventory.service';
import { MasterDataController } from './master-data/master-data.controller';
import { MasterDataService } from './master-data/master-data.service';
import { OrdersController } from './orders/orders.controller';
import { OrdersService } from './orders/orders.service';
import { ProductionController } from './production/production.controller';
import { ProductionService } from './production/production.service';
import { FinanceController } from './finance/finance.controller';
import { FinanceService } from './finance/finance.service';

@Module({
  controllers: [
    HealthController,
    AuthController,
    MasterDataController,
    InventoryController,
    OrdersController,
    ProductionController,
    FinanceController,
  ],
  providers: [AuthService, MasterDataService, InventoryService, OrdersService, ProductionService, FinanceService],
})
export class AppModule {}
