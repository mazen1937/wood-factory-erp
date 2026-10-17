import { Module } from '@nestjs/common';
import { HealthController } from './health.controller';
import { AuthController } from './auth/auth.controller';
import { AuthService } from './auth/auth.service';
@Module({ controllers: [HealthController, AuthController], providers: [AuthService] })
export class AppModule {}
