import {SellingPricesService} from './selling-prices.service';
import {Body,Controller,Get,Headers,Param,ParseUUIDPipe,Post,Query,UnauthorizedException} from '@nestjs/common';
import {MonthlyFinanceService} from './monthly-finance.service';
import {AuthService} from '../auth/auth.service';
import {FinanceService} from './finance.service';
import {FinanceCommandDto,FinanceConfigDto,SellingPriceDto} from './finance.dto';
@Controller('finance')
export class FinanceController {
 constructor(private readonly auth:AuthService,private readonly finance:FinanceService,private readonly monthlyFinance:MonthlyFinanceService,private readonly sellingPrices:SellingPricesService){}
 private async actor(header:string){const token=header?.replace(/^Bearer\s+/i,'');if(!token)throw new UnauthorizedException();return (await this.auth.me(token)).sub as string;}
 @Get() async report(@Headers('authorization') header:string){return this.finance.report(await this.actor(header));}
 @Get('monthly') async monthly(@Headers('authorization') header:string,@Query('month') month:string){return this.monthlyFinance.monthly(await this.actor(header),month);}
 @Get('orders/:id/activity') async orderActivity(@Headers('authorization') header:string,@Param('id',new ParseUUIDPipe()) id:string){return this.monthlyFinance.order(await this.actor(header),id);}
 @Post('prices') async savePrice(@Headers('authorization') header:string,@Body() body:SellingPriceDto){return this.sellingPrices.save(body,await this.actor(header));}
 @Post('documents') async post(@Headers('authorization') header:string,@Body() body:FinanceCommandDto){return this.finance.execute(body,await this.actor(header));}
 @Post('configuration') async configure(@Headers('authorization') header:string,@Body() body:FinanceConfigDto){return this.finance.configure(body,await this.actor(header));}
}
