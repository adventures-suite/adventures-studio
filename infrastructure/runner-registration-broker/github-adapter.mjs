const owner='adventures-suite', repository='adventures-studio', prefix=`/repos/${owner}/${repository}/actions/runners`;
export class GitHubRunnerAdapter {
  constructor(transport, installationTokenFactory) { this.transport=transport; this.installationTokenFactory=installationTokenFactory; }
  async request(method,path,body,signal) {
    const allowed=(method==='POST'&&path===`${prefix}/generate-jitconfig`)||(method==='GET'&&path===prefix)||(method==='DELETE'&&new RegExp(`^${prefix}/[1-9][0-9]*$`).test(path));
    if (!allowed) throw new Error('github-endpoint-denied');
    const token=await this.installationTokenFactory.create({repositoryIds:[1317655952],permissions:{administration:'write'},maximumSeconds:3600},signal);
    try { return await token.use(credential=>this.transport.send({origin:'https://api.github.com',method,path,body,credential,followRedirects:false},signal)); }
    finally { token.dispose(); }
  }
  async generateJitConfiguration(binding,signal) { const r=await this.request('POST',`${prefix}/generate-jitconfig`,{name:binding.runnerName,runner_group_id:binding.runnerGroupId,labels:binding.labels,work_folder:'_work'},signal); return r.encoded_jit_config; }
  async listRunners(signal) { const r=await this.request('GET',prefix,null,signal); if(!Array.isArray(r.runners)||r.runners.length>100) throw new Error('runner-list-bounds'); return r.runners; }
  async deleteRunner(id,signal) { if(!Number.isSafeInteger(id)||id<=0) throw new Error('runner-id'); return this.request('DELETE',`${prefix}/${id}`,null,signal); }
}
