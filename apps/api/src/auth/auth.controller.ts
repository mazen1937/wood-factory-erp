import { Body, Controller, Get, HttpCode, HttpException, HttpStatus, Post, Req } from '@nestjs/common';
import { IsString, MinLength } from 'class-validator';
import { AuthService } from './auth.service';

class LoginRequest { @IsString() username!: string; @IsString() @MinLength(8) password!: string; }
@Controller('auth')
export class AuthController {
  constructor(private readonly auth: AuthService) {}
  @Post('login') @HttpCode(200) login(@Body() body: LoginRequest) { return this.auth.login(body.username, body.password); }
  @Get('me') me(@Req() request: { headers: Record<string, string | undefined> }) {
    const token = request.headers.authorization?.replace(/^Bearer\s+/i, '');
    if (!token) throw new HttpException('Missing bearer token', HttpStatus.UNAUTHORIZED);
    return this.auth.me(token);
  }
}
