import { Body, Controller, ForbiddenException, Get, Headers, Param, ParseUUIDPipe, Post, Query, UnauthorizedException } from '@nestjs/common';
import { AuthService } from '../auth/auth.service';
import { ProductionService } from './production.service';
import { ExecutionCommandDto, LaborRateDto, WorkerDto } from './production.dto';

@Controller('production')
export class ProductionController {
  constructor(private readonly auth: AuthService, private readonly service: ProductionService) {}
  private async actor(authorization: string, configure = false) {
    const token = authorization?.replace(/^Bearer\s+/i,'');
    if (!token) throw new UnauthorizedException();
    const user = await this.auth.me(token);
    // Resolve current active permissions in PostgreSQL for every official posting;
    // an old token cannot retain revoked production access.
    await this.service.authorize(user.sub, configure);
    return user.sub as string;
  }
  @Get()
  async list(@Headers('authorization') authorization: string) { await this.actor(authorization); return this.service.list(); }
  @Get('catalog')
  async catalog(@Headers('authorization') authorization: string) { await this.actor(authorization); return this.service.catalog(); }
  @Get('workers')
  async workers(@Headers('authorization') authorization:string){await this.actor(authorization);return this.service.workers();}
  @Post('workers')
  async addWorker(@Headers('authorization') authorization:string,@Body() body:WorkerDto){return this.service.addWorker(body,await this.actor(authorization,true));}
  @Get('labor-rates')
  async rates(@Headers('authorization') authorization: string) { await this.actor(authorization); return this.service.rates(); }
  @Post('labor-rates')
  async addRate(@Headers('authorization') authorization: string,@Body() body: LaborRateDto) {
    return this.service.addRate(body,await this.actor(authorization,true));
  }
  @Get(':id')
  async detail(@Headers('authorization') authorization: string,@Param('id',new ParseUUIDPipe()) id:string,@Query('month') month?:string) {
    await this.actor(authorization); return this.service.detail(id,month);
  }
  @Post(':id/events')
  async post(@Headers('authorization') authorization:string,@Param('id',new ParseUUIDPipe()) id:string,@Body() body:ExecutionCommandDto) {
    return this.service.execute(id,body,await this.actor(authorization,body.action==='wage_agreement'));
  }
}
