export class GitHubAppInstallationTokenFactory {
  constructor(appId,installationId,keyLoader,jwtSigner,transport) {
    if(!/^[1-9][0-9]*$/.test(String(appId))||!/^[1-9][0-9]*$/.test(String(installationId))) throw new Error('github-app-live-id-required');
    this.appId=String(appId); this.installationId=String(installationId); this.keyLoader=keyLoader; this.jwtSigner=jwtSigner; this.transport=transport;
  }
  async create(scope,signal) {
    if(JSON.stringify(scope.repositoryIds)!=='[1317655952]'||JSON.stringify(scope.permissions)!=='{"administration":"write"}'||scope.maximumSeconds!==3600) throw new Error('installation-token-scope-denied');
    return this.keyLoader.use(async key => {
      const now=Math.floor(Date.now()/1000); const appJwt=await this.jwtSigner.sign({alg:'RS256',typ:'JWT'},{iss:this.appId,iat:now-30,exp:now+540},key,signal);
      try {
        const response=await this.transport.send({origin:'https://api.github.com',method:'POST',path:`/app/installations/${this.installationId}/access_tokens`,credential:appJwt,body:{repository_ids:[1317655952],permissions:{administration:'write'}},followRedirects:false},signal);
        if(typeof response.token!=='string'||response.token.length<16||Date.parse(response.expires_at)<=Date.now()) throw new Error('installation-token-response');
        return Object.freeze({use:callback=>callback(response.token),dispose:()=>{ response.token=undefined; }});
      } finally { appJwt?.dispose?.(); }
    },signal);
  }
}
