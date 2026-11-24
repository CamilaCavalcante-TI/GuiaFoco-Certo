/*!
 * GuiaFoco – cliente da API (JavaScript puro, sem dependências).
 *
 * Como usar no seu index.html, ANTES do seu script principal:
 *   <script src="guiafoco-api.js"></script>
 *   <script>
 *     GuiaFocoAPI.configure({ baseUrl: 'https://SUA-API.exemplo.com' });
 *   </script>
 *
 * Todas as funções retornam Promises e lançam GuiaFocoAPI.ApiError em caso de erro.
 */
(function (global) {
  'use strict';

  const cfg = {
    baseUrl: 'https://localhost:7080',
    storagePrefix: 'guiafoco.'
  };

  class ApiError extends Error {
    constructor(status, message, detalhes) {
      super(message);
      this.name = 'ApiError';
      this.status = status;
      this.detalhes = detalhes || null; // erros de validação por campo
    }
  }

  // ---------- armazenamento da sessão ----------
  const store = {
    get(k) { try { return localStorage.getItem(cfg.storagePrefix + k); } catch { return null; } },
    set(k, v) { try { localStorage.setItem(cfg.storagePrefix + k, v); } catch { /* ignore */ } },
    del(k) { try { localStorage.removeItem(cfg.storagePrefix + k); } catch { /* ignore */ } }
  };

  function salvarAuth(auth) {
    store.set('token', auth.token);
    store.set('expiraEm', auth.expiraEm);
    store.set('responsavel', JSON.stringify(auth.responsavel));
    if (auth.crianca) store.set('criancaId', String(auth.crianca.id));
    return auth;
  }

  // ---------- HTTP ----------
  async function http(method, path, body) {
    const headers = { 'Accept': 'application/json' };
    const token = store.get('token');
    if (token) headers['Authorization'] = 'Bearer ' + token;
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    let res;
    try {
      res = await fetch(cfg.baseUrl.replace(/\/$/, '') + path, {
        method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : undefined
      });
    } catch (e) {
      throw new ApiError(0, 'Não foi possível conectar ao servidor. Verifique sua conexão.');
    }

    if (res.status === 401 && token) {
      api.logout();
      global.dispatchEvent(new CustomEvent('guiafoco:unauthorized'));
    }

    if (res.status === 204) return null;

    let data = null;
    const texto = await res.text();
    if (texto) { try { data = JSON.parse(texto); } catch { data = texto; } }

    if (!res.ok) {
      const msg = (data && (data.title || data.detail)) || 'Erro ' + res.status;
      throw new ApiError(res.status, msg, data && data.errors);
    }
    return data;
  }

  // ---------- API pública ----------
  const api = {
    ApiError,

    configure(opts) { Object.assign(cfg, opts || {}); return api; },

    // --- Sessão do usuário ---
    isLogged() {
      const exp = store.get('expiraEm');
      return !!store.get('token') && (!exp || new Date(exp) > new Date());
    },
    token() { return store.get('token'); },
    responsavel() { try { return JSON.parse(store.get('responsavel')); } catch { return null; } },
    criancaId() { const v = store.get('criancaId'); return v ? Number(v) : null; },
    logout() { ['token', 'expiraEm', 'responsavel', 'criancaId', 'sessaoId'].forEach(store.del); },

    // --- Autenticação ---
    async login(email, senha) { return salvarAuth(await http('POST', '/api/auth/login', { email, senha })); },
    async demo() { return salvarAuth(await http('POST', '/api/auth/demo')); },
    /**
     * form = {
     *   nomeResponsavel, email, senha, nomeCrianca, idade, serie, tempoTelaEstimadoHoras,
     *   limiteDiarioMin, inicioPermitido: '18:00', fimPermitido: '21:00',
     *   preferencias: ['Memória','Puzzle'], intervencaoAdaptativa, intervirAposMin, bloqueioSimulado
     * }
     */
    async register(form) { return salvarAuth(await http('POST', '/api/auth/register', form)); },
    me() { return http('GET', '/api/auth/me'); },

    // --- Criança ---
    criancas() { return http('GET', '/api/criancas'); },
    crianca(id) { return http('GET', `/api/criancas/${id ?? api.criancaId()}`); },
    atualizarCrianca(id, dados) { return http('PUT', `/api/criancas/${id}`, dados); },
    salvarConfiguracoes(id, config) { return http('PUT', `/api/criancas/${id}/configuracoes`, config); },

    // --- Painel do responsável ---
    resumo(id) { return http('GET', `/api/criancas/${id ?? api.criancaId()}/resumo`); },
    desempenho(id) { return http('GET', `/api/criancas/${id ?? api.criancaId()}/desempenho`); },
    usoHoje(id) { return http('GET', `/api/criancas/${id ?? api.criancaId()}/uso-hoje`); },
    recomendacao(id) { return http('GET', `/api/criancas/${id ?? api.criancaId()}/recomendacao`); },

    // --- Sessões de uso ---
    async iniciarSessao(criancaId) {
      const s = await http('POST', '/api/sessoes', { criancaId: criancaId ?? api.criancaId() });
      store.set('sessaoId', String(s.id));
      return s;
    },
    async encerrarSessao(sessaoId, abandonada = false) {
      const id = sessaoId ?? api.sessaoId();
      if (!id) return null;
      const s = await http('PATCH', `/api/sessoes/${id}/encerrar`, { abandonada });
      store.del('sessaoId');
      return s;
    },
    registrarIntervencao(sessaoId) { return http('POST', `/api/sessoes/${sessaoId ?? api.sessaoId()}/intervencao`); },
    sessaoId() { const v = store.get('sessaoId'); return v ? Number(v) : null; },

    // --- Desafios ---
    desafios() { return http('GET', '/api/desafios'); },
    perguntas(desafioId, quantidade) {
      return http('GET', `/api/desafios/${desafioId}/perguntas` + (quantidade ? `?quantidade=${quantidade}` : ''));
    },
    /** tempoRespostaMs = Date.now() - momentoEmQueAPerguntaFoiExibida */
    responder({ perguntaId, opcaoId, tempoRespostaMs, criancaId, sessaoId }) {
      return http('POST', '/api/respostas', {
        criancaId: criancaId ?? api.criancaId(),
        perguntaId,
        opcaoId,
        sessaoId: sessaoId ?? api.sessaoId() ?? null,
        tempoRespostaMs: Math.max(0, Math.round(tempoRespostaMs || 0))
      });
    },

    /**
     * Consulta /uso-hoje periodicamente e chama onIntervir(uso) quando a API
     * indicar que o aviso "Hora de uma pausa!" deve aparecer. Retorna uma função para parar.
     */
    monitorarUso(onIntervir, { intervaloSeg = 30, onAtualizar } = {}) {
      let ativo = true;
      let avisado = false;
      const tick = async () => {
        if (!ativo) return;
        try {
          const uso = await api.usoHoje();
          if (onAtualizar) onAtualizar(uso);
          if (uso.deveIntervir && !avisado) {
            avisado = true;
            if (uso.sessaoAbertaId) api.registrarIntervencao(uso.sessaoAbertaId).catch(() => {});
            onIntervir(uso);
          }
          if (!uso.deveIntervir) avisado = false;
        } catch (e) { /* ignora falhas temporárias de rede */ }
      };
      tick();
      const timer = setInterval(tick, intervaloSeg * 1000);
      return () => { ativo = false; clearInterval(timer); };
    }
  };

  global.GuiaFocoAPI = api;
})(window);
