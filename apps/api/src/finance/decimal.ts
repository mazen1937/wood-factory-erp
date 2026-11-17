// All financial values cross the API as decimal strings. Never convert money to Number.
export const SCALE=10000n;
export class DecimalInputError extends Error {}
export function units(value:unknown,positive=false):bigint {
 if(typeof value!=='string'||!/^\d{1,12}(\.\d{1,4})?$/.test(value)) throw new DecimalInputError('أدخل رقماً عشرياً صحيحاً حتى ٤ منازل، دون فواصل آلاف.');
 const [a,b='']=value.split('.');const result=BigInt(a)*SCALE+BigInt(b.padEnd(4,'0'));
 if(positive&&result<=0n)throw new DecimalInputError('يجب أن تكون القيمة أكبر من صفر.');return result;
}
export function decimal(value:bigint):string {const sign=value<0n?'-':'';const n=value<0n?-value:value;return sign+(n/SCALE)+'.'+(n%SCALE).toString().padStart(4,'0');}
export function rounded(n:bigint,d:bigint):bigint {if(d<=0n)throw new Error('Invalid divisor');return n<0n?-rounded(-n,d):(n+d/2n)/d;}
export function multiply(a:bigint,b:bigint){return rounded(a*b,SCALE);}
export function prorate(total:bigint,quantity:bigint,original:bigint){return rounded(total*quantity,original);}
export function percentage(net:bigint,rate:bigint){return rounded(net*rate,100n*SCALE);}
export function canonical(value:any):string {if(Array.isArray(value))return '['+value.map(canonical).join(',')+']';if(value&&typeof value==='object')return '{'+Object.keys(value).sort().filter(k=>value[k]!==undefined).map(k=>JSON.stringify(k)+':'+canonical(value[k])).join(',')+'}';return JSON.stringify(value);}
