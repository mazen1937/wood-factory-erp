import {Body,Controller,Get,Headers,Post,UnauthorizedException} from '@nestjs/common';
import {AuthService} from '../auth/auth.service';
import {FinanceService} from './finance.service';
import {FinanceCommandDto,FinanceConfigDto} from './finance.dto';
@Controller('finance')
export class FinanceController {
 constructor(private readonly auth:AuthService,private readonly finance:FinanceService){}
 private async actor(header:string){const token=header?.replace(/^Bearer\s+/i,'');if(!token)throw new UnauthorizedException();return (await this.auth.me(token)).sub as string;}
 @Get() async report(@Headers('authorization') header:string){return this.finance.report(await this.actor(header));}
 @Post('documents') async post(@Headers('authorization') header:string,@Body() body:FinanceCommandDto){return this.finance.execute(body,await this.actor(header));}
 @Post('configuration') async configure(@Headers('authorization') header:string,@Body() body:FinanceConfigDto){return this.finance.configure(body,await this.actor(header));}
}
