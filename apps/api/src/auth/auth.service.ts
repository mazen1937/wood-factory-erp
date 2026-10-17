import { HttpException, HttpStatus, Injectable, OnModuleInit } from '@nestjs/common';
import { createHmac, randomBytes, scryptSync, timingSafeEqual } from 'node:crypto';
import { Pool } from 'pg';

@Injectable()
export class AuthService implements OnModuleInit {
  private readonly pool = new Pool({ connectionString: process.env.DATABASE_URL ?? 'postgresql://erp_app:change-me-for-local-development@localhost:5432/wood_factory_erp' });
  private readonly secret = process.env.AUTH_TOKEN_SECRET ?? 'development-only-change-me';
  async onModuleInit(): Promise<void> { await this.bootstrapAdmin(); }
  private async bootstrapAdmin(): Promise<void> {
    const username=process.env.BOOTSTRAP_ADMIN_USERNAME; const password=process.env.BOOTSTRAP_ADMIN_PASSWORD;
    if (!username || !password) return;
    const existing=await this.pool.query('select id from users where username=$1',[username]); if (existing.rowCount) return;
    const salt=randomBytes(16).toString('hex'); const hash=scryptSync(password,salt,64).toString('hex');
    const client=await this.pool.connect(); try { await client.query('begin'); const role=await client.query("insert into roles (code, name) values ('system_admin','System Administrator') on conflict (code) do update set code=excluded.code returning id"); const user=await client.query('insert into users (username,password_hash,display_name,active) values ($1,$2,$3,true) returning id',[username,`${salt}:${hash}`,username]); await client.query('insert into user_roles (user_id,role_id) values ($1,$2)',[user.rows[0].id,role.rows[0].id]); await client.query('commit'); } catch(e) { await client.query('rollback'); throw e; } finally { client.release(); }
  }
  async login(username:string,password:string) { const result=await this.pool.query('select u.id,u.username,u.display_name,u.password_hash,array_remove(array_agg(r.code),null) roles from users u left join user_roles ur on ur.user_id=u.id left join roles r on r.id=ur.role_id where u.username=$1 and u.active=true group by u.id',[username]); const user=result.rows[0]; if (!user || !this.matches(password,user.password_hash)) throw new HttpException('Invalid username or password',HttpStatus.UNAUTHORIZED); const payload=Buffer.from(JSON.stringify({sub:user.id,username:user.username,roles:user.roles ?? [],exp:Math.floor(Date.now()/1000)+28800})).toString('base64url'); const signature=createHmac('sha256',this.secret).update(payload).digest('base64url'); return {accessToken:`${payload}.${signature}`,user:{id:user.id,username:user.username,displayName:user.display_name,roles:user.roles ?? []}}; }
  async me(token:string) { const [payload,signature]=token.split('.'); if (!payload || !signature || !timingSafeEqual(Buffer.from(signature),Buffer.from(createHmac('sha256',this.secret).update(payload).digest('base64url')))) throw new HttpException('Invalid token',HttpStatus.UNAUTHORIZED); const value=JSON.parse(Buffer.from(payload,'base64url').toString()); if (value.exp < Math.floor(Date.now()/1000)) throw new HttpException('Session expired',HttpStatus.UNAUTHORIZED); return value; }
  private matches(password:string,stored:string) { const [salt,hash]=stored.split(':'); const actual=scryptSync(password,salt,64); return timingSafeEqual(actual,Buffer.from(hash,'hex')); }
}
