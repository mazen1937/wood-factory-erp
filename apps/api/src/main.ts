import { config } from 'dotenv';
import { resolve } from 'node:path';
config({ path: resolve(process.cwd(), '../../.env') });

import { ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap(): Promise<void> {
  const app = await NestFactory.create(AppModule);
  app.setGlobalPrefix('api');
  app.useGlobalPipes(new ValidationPipe({ whitelist: true, transform: true }));
  await app.listen(process.env.API_PORT ?? 3000);
}
void bootstrap();

