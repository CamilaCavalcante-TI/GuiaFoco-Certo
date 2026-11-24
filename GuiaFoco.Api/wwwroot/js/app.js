// GuiaFoco — front ligado à API (C# + MySQL).
// Os dados vêm da API através do arquivo guiafoco-api.js. O localStorage guarda só o login
// e as preferências visuais, para a tela abrir já no modo certo.

const $ = (s) => document.querySelector(s);
const $$ = (s) => [...document.querySelectorAll(s)];

const API = window.GuiaFocoAPI;
API.configure({ baseUrl: window.GUIAFOCO_API_URL || "http://localhost:5080" });

const state = {
  responsavel: null,
  crianca: null,
  pararMonitor: null,
  desafioPalavrasId: null,
  perguntas: [],
  indice: 0,
  acertos: 0,
  exibidaEm: 0,
  timerBase: 0,
  timerInicio: 0,
  sessaoAberta: false
};

// ---------------------------------------------------------------- utilidades

function toast(msg) {
  const el = $("#toast");
  el.textContent = msg;
  el.classList.add("show");
  clearTimeout(toast._t);
  toast._t = setTimeout(() => el.classList.remove("show"), 3000);
}

const fmtMin = (min) => {
  min = Math.max(0, Math.round(min || 0));
  return `${Math.floor(min / 60)}h${String(min % 60).padStart(2, "0")}`;
};
const fmtNum = (v, casas = 1) => Number(v || 0).toLocaleString("pt-BR", { maximumFractionDigits: casas });
const fmtSinal = (v) => `${v > 0 ? "+" : ""}${v}%`;
const fmtHorario = (hhmm) => {
  const [h, m] = String(hhmm || "00:00").split(":");
  return m === "00" ? `${Number(h)}h` : `${Number(h)}h${m}`;
};
const setText = (sel, txt) => { const el = $(sel); if (el) el.textContent = txt; };
const setBar = (sel, pct) => { const el = $(sel); if (el) el.style.width = `${Math.max(0, Math.min(100, pct || 0))}%`; };
const escapeHtml = (t) => String(t).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

const NOMES_CAMPOS = {
  email: "e-mail", senha: "senha (mínimo 6 caracteres)", nomeresponsavel: "nome do responsável",
  nomecrianca: "nome da criança", idade: "idade (3 a 12 anos)", serie: "série escolar",
  tempotelaestimadohoras: "tempo de tela estimado (0 a 24 h)", limitediariomin: "limite diário (5 a 720 min)",
  iniciopermitido: "início permitido", fimpermitido: "fim permitido", interviraposmin: "intervir após (1 a 180 min)",
  fontepercentual: "tamanho da fonte"
};

function mensagemErro(e) {
  if (!(e instanceof API.ApiError)) return "Ocorreu um erro inesperado.";
  if (e.status === 0) return "Não foi possível conectar à API. Confira se ela está rodando (dotnet run).";
  if (e.detalhes && typeof e.detalhes === "object") {
    const campos = Object.keys(e.detalhes).map((k) => NOMES_CAMPOS[k.replace(/^\$\./, "").toLowerCase()] || k);
    return `Confira: ${[...new Set(campos)].join(", ")}.`;
  }
  return e.message;
}

async function comBotao(btn, fn) {
  if (btn) btn.disabled = true;
  try { return await fn(); }
  catch (e) { toast(mensagemErro(e)); }
  finally { if (btn) btn.disabled = false; }
}

// ---------------------------------------------------------------- telas

const pageNames = { dashboard: "Visão geral", crianca: "Perfil da criança", desempenho: "Desempenho", tempo: "Tempo de tela", desafios: "Microdesafios", config: "Configurações" };

function showPage(name) {
  $$(".page").forEach((p) => p.classList.toggle("active", p.id === `page-${name}`));
  $$(".nav-item").forEach((b) => b.classList.toggle("active", b.dataset.page === name));
  $("#pageTitle").textContent = pageNames[name] || "GuiaFoco";
  if (innerWidth <= 780) $("#sidebar").classList.remove("open");
}

function mostrarLogin() {
  $("#appScreen").classList.add("hidden");
  $("#registerScreen").classList.add("hidden");
  $("#loginScreen").classList.remove("hidden");
}

function openApp() {
  $("#loginScreen").classList.add("hidden");
  $("#registerScreen").classList.add("hidden");
  $("#appScreen").classList.remove("hidden");
  showPage("dashboard");
}

// ---------------------------------------------------------------- renderização

function renderPerfil() {
  const r = state.responsavel, c = state.crianca, cfg = c.configuracao;
  const primeiroNome = (r.nome || "Responsável").split(" ")[0];
  setText("#responsavelLabel", primeiroNome);
  setText("#avatar", primeiroNome.charAt(0).toUpperCase());
  setText("#childLabel", c.nome);
  setText("#profileChildName", c.nome);
  setText("#profileChildMeta", `${c.idade} anos · ${c.serie}`);
  setText("#challengeChildName", c.nome);
  setText("#detailAge", `${c.idade} anos`);
  setText("#detailSeries", c.serie);
  setText("#detailEstimated", `${fmtNum(c.tempoTelaEstimadoHoras)} h/dia`);
  setText("#detailLimit", fmtMin(cfg.limiteDiarioMin));
  setText("#detailSchedule", `${fmtHorario(cfg.inicioPermitido)}–${fmtHorario(cfg.fimPermitido)}`);
  setText("#detailPrefs", cfg.preferencias.length ? cfg.preferencias.join(" · ") : "—");
  setText("#settingSchedule", `${fmtHorario(cfg.inicioPermitido)}–${fmtHorario(cfg.fimPermitido)}`);
  setText("#pointsLabel", `⭐ ${c.pontos} pontos`);
  setText("#interventionStatus", cfg.intervencaoAdaptativa ? "Ativa" : "Desativada");
  $("#settingIntervention").checked = cfg.intervencaoAdaptativa;
  $("#settingMinutes").value = cfg.intervirAposMin;
  $("#settingBlock").checked = cfg.bloqueioSimulado;
}

function renderResumo(r) {
  setText("#statAcertos", `${r.taxaAcertos}%`);
  setText("#statAcertosVar", `${fmtSinal(r.variacaoAcertosSemana)} esta semana`);
  setText("#statSessao", `${fmtNum(r.sessaoMediaMin, 0)} min`);
  setText("#statSessoesHoje", `${r.sessoesHoje} ${r.sessoesHoje === 1 ? "sessão" : "sessões"} hoje`);
  setText("#statEvolucao", fmtSinal(r.evolucaoSemanal));
  setText("#statEvolucaoTxt", r.evolucaoSemanal > 0 ? "Desempenho crescente" : r.evolucaoSemanal < 0 ? "Desempenho em queda" : "Desempenho estável");
  setText("#statConclusao", `${r.conclusao}%`);
  setText("#statAbandono", `${r.abandono}% de abandono`);

  const pct = r.trilha.progressoPercentual;
  setText("#trilhaNivel", r.trilha.nivel);
  setBar("#trilhaBar", pct);
  setText("#trilhaPct", `${pct}%`);
  const etapas = ["Vogais", "Letras", "BA / CA", "Palavras"];
  const atual = Math.min(etapas.length, Math.floor(pct / 25));
  $("#learningPath").innerHTML = etapas.map((nome, i) =>
    i < atual ? `<span class="done">✓ ${nome}</span>` : i === atual ? `<span class="current">${nome}</span>` : `<span>${nome}</span>`
  ).join("");

  setText("#dashRecNivel", `Nível recomendado: ${r.recomendacao.nivel.toLowerCase()}`);
  setText("#dashRecTexto", r.recomendacao.titulo + ". " + "As próximas atividades são ajustadas com base no tempo de resposta, acertos e erros consecutivos.");
  renderUso(r.usoHoje);
}

function renderDesempenho(d) {
  setText("#mDuracao", `${fmtNum(d.uso.duracaoMediaSessaoMin, 0)} min`);
  setText("#mSessoesDia", fmtNum(d.uso.sessoesPorDia));
  setText("#mHorario", d.uso.horarioSessoes);
  setText("#mMinigame", `${fmtNum(d.uso.tempoPorMinigameMin)} min`);
  setText("#mConclusao", `${d.desempenho.taxaConclusao}%`);
  setText("#mAbandono", `${d.desempenho.taxaAbandono}%`);
  setText("#mTentativas", fmtNum(d.desempenho.tentativasMedias));
  setText("#mAcertos", `${d.desempenho.acertos}%`);
  setText("#mErros", fmtNum(d.desempenho.errosConsecutivos));
  setText("#mTempoResp", `${fmtNum(d.desempenho.tempoRespostaSeg)} s`);
  setText("#mTrocas", d.comportamento.trocasFrequentes);
  setText("#mRetorno", `${d.comportamento.retornoAposEncerramento} ${d.comportamento.retornoAposEncerramento === 1 ? "vez" : "vezes"}`);
  setText("#mRespeito", d.comportamento.respeitoAoLimite);
  setText("#mEvolucao", fmtSinal(d.comportamento.evolucaoSemanal));
  setText("#mPrefs", d.comportamento.preferencias);
  setText("#recNivel", d.recomendacao.nivel);
  setText("#recTitulo", d.recomendacao.titulo);
  setText("#recTexto", d.recomendacao.texto);
}

function renderUso(u) {
  if (!u) return;
  const pct = u.percentualUsado;
  setText("#usedTime", fmtMin(u.usadoMin));
  setText("#limitTime", `/ ${fmtMin(u.limiteMin)}`);
  setBar("#tempoBar", pct);
  $("#restanteTxt").innerHTML = u.limiteExcedido
    ? "O limite diário <strong>já foi atingido</strong>."
    : `Restam aproximadamente <strong>${u.restanteMin} minutos</strong> do limite diário.`;
  setText("#dashUso", `${fmtMin(u.usadoMin)} / ${fmtMin(u.limiteMin)}`);
  setBar("#dashUsoBar", pct);
  setText("#dashUsoPct", `${pct}% do limite utilizado`);

  state.sessaoAberta = !!u.sessaoAbertaId;
  state.timerBase = u.sessaoAtualSegundos || 0;
  state.timerInicio = Date.now();
  atualizarTimer();
}

function atualizarTimer() {
  const total = state.sessaoAberta ? state.timerBase + Math.floor((Date.now() - state.timerInicio) / 1000) : 0;
  const m = Math.floor(total / 60), s = total % 60;
  setText("#sessionTimer", `${m}:${String(s).padStart(2, "0")}`);
}
setInterval(atualizarTimer, 1000);

// Ao sair, apaga os números da conta anterior para não aparecerem na próxima.
function limparPainel() {
  ["#statAcertos", "#statSessao", "#statEvolucao", "#statConclusao", "#trilhaNivel", "#recNivel", "#recTitulo",
   "#mDuracao", "#mSessoesDia", "#mHorario", "#mMinigame", "#mConclusao", "#mAbandono", "#mTentativas", "#mAcertos",
   "#mErros", "#mTempoResp", "#mTrocas", "#mRetorno", "#mRespeito", "#mEvolucao", "#mPrefs"].forEach((s) => setText(s, "–"));
  ["#statAcertosVar", "#statSessoesHoje", "#statEvolucaoTxt", "#statAbandono", "#recTexto"].forEach((s) => setText(s, " "));
  setText("#dashUso", "– / –");
  setText("#trilhaPct", "0%");
  ["#trilhaBar", "#dashUsoBar", "#tempoBar"].forEach((s) => setBar(s, 0));
  $("#challengeArea").classList.add("hidden");
  state.desafioPalavrasId = null;
}

async function carregarPainel() {
  try {
    const [resumo, desempenho] = await Promise.all([API.resumo(), API.desempenho()]);
    renderResumo(resumo);
    renderDesempenho(desempenho);
  } catch (e) {
    toast(mensagemErro(e));
  }
}

// ---------------------------------------------------------------- login / cadastro

async function entrar(auth) {
  if (!auth.crianca) { toast("Nenhuma criança cadastrada nesta conta."); return; }
  state.responsavel = auth.responsavel;
  state.crianca = auth.crianca;
  aplicarAcessibilidade(auth.crianca.configuracao);
  renderPerfil();
  openApp();
  try { await API.iniciarSessao(); } catch (e) { /* o painel continua funcionando */ }
  await carregarPainel();
  iniciarMonitor();
}

$("#showRegister").addEventListener("click", () => { $("#loginScreen").classList.add("hidden"); $("#registerScreen").classList.remove("hidden"); });
$("#backToLogin").addEventListener("click", () => { $("#registerScreen").classList.add("hidden"); $("#loginScreen").classList.remove("hidden"); });

$("#loginForm").addEventListener("submit", (e) => {
  e.preventDefault();
  const btn = e.submitter || $("#loginForm button[type=submit]");
  comBotao(btn, async () => {
    const auth = await API.login($("#loginEmail").value.trim(), $("#loginPassword").value);
    $("#loginPassword").value = "";
    await entrar(auth);
    toast("Login realizado com sucesso!");
  });
});

$("#demoLogin").addEventListener("click", (e) => comBotao(e.currentTarget, async () => {
  await entrar(await API.demo());
  toast("Modo demonstração iniciado.");
}));

$("#registerForm").addEventListener("submit", (e) => {
  e.preventDefault();
  const btn = e.submitter || $("#registerForm button[type=submit]");
  comBotao(btn, async () => {
    const auth = await API.register({
      nomeResponsavel: $("#responsavelNome").value.trim(),
      email: $("#responsavelEmail").value.trim(),
      senha: $("#responsavelSenha").value,
      nomeCrianca: $("#criancaNome").value.trim(),
      idade: Number($("#criancaIdade").value),
      serie: $("#criancaSerie").value,
      tempoTelaEstimadoHoras: Number($("#tempoEstimado").value),
      limiteDiarioMin: Number($("#limiteDiario").value),
      inicioPermitido: $("#inicioPermitido").value || "18:00",
      fimPermitido: $("#fimPermitido").value || "21:00",
      preferencias: $$('input[name="pref"]:checked').map((x) => x.value),
      intervencaoAdaptativa: $("#ativarIntervencao").checked,
      intervirAposMin: Number($("#intervencaoMin").value),
      bloqueioSimulado: $("#ativarBloqueio").checked
    });
    $("#responsavelSenha").value = "";
    await entrar(auth);
    toast("Perfil criado! Bem-vindo ao GuiaFoco.");
  });
});

async function sair(msg) {
  if (state.pararMonitor) { state.pararMonitor(); state.pararMonitor = null; }
  try { await API.encerrarSessao(); } catch (e) { /* ignora */ }
  API.logout();
  limparPainel();
  state.responsavel = state.crianca = null;
  state.sessaoAberta = false;
  closeModal();
  mostrarLogin();
  toast(msg || "Sessão encerrada.");
}

$("#logoutBtn").addEventListener("click", () => sair());
window.addEventListener("guiafoco:unauthorized", () => sair("Sua sessão expirou. Entre novamente."));

// ---------------------------------------------------------------- navegação

$$(".nav-item").forEach((btn) => btn.addEventListener("click", () => showPage(btn.dataset.page)));
$$("[data-page-jump]").forEach((btn) => btn.addEventListener("click", () => showPage(btn.dataset.pageJump)));
$("#menuBtn").addEventListener("click", () => $("#sidebar").classList.toggle("open"));
$$('.nav-item[data-page="desempenho"], .nav-item[data-page="dashboard"]').forEach((b) =>
  b.addEventListener("click", () => { if (state.crianca) carregarPainel(); }));

// ---------------------------------------------------------------- configurações (salvas na API)

// As alterações entram na hora na tela e são enviadas à API uma de cada vez (em fila),
// para que dois cliques rápidos não sobrescrevam um ao outro.
let filaConfig = Promise.resolve();

function salvarConfig(alteracoes, msg) {
  if (!state.crianca) return;
  const crianca = state.crianca;
  Object.assign(crianca.configuracao, alteracoes);
  renderPerfil();
  filaConfig = filaConfig.then(async () => {
    try {
      await API.salvarConfiguracoes(crianca.id, { ...crianca.configuracao });
      if (msg) toast(msg);
    } catch (e) {
      toast(mensagemErro(e));
      try { crianca.configuracao = (await API.crianca(crianca.id)).configuracao; } catch (_) { /* ignora */ }
      if (state.crianca === crianca) renderPerfil(); // volta os campos para o valor salvo
    }
  });
}

$("#settingIntervention").addEventListener("change", (e) => salvarConfig({ intervencaoAdaptativa: e.target.checked }, "Configuração atualizada."));
$("#settingBlock").addEventListener("change", (e) => salvarConfig({ bloqueioSimulado: e.target.checked }, "Configuração de bloqueio atualizada."));
$("#settingMinutes").addEventListener("change", (e) => {
  const v = Number(e.target.value);
  if (!(v >= 1 && v <= 180)) { toast("Use um valor entre 1 e 180 minutos."); renderPerfil(); return; }
  salvarConfig({ intervirAposMin: v }, "Tempo de intervenção atualizado.");
});

// Acessibilidade: aplica na hora, guarda no navegador e salva na API.
let fontScale = Number(localStorage.getItem("guiaFontScale") || 1);

function setDark(on, salvar = true) {
  document.body.classList.toggle("dark", on);
  $("#accessDark").checked = on;
  localStorage.setItem("guiaDark", on ? "1" : "0");
  if (salvar) salvarConfig({ modoEscuro: on });
}
function setContrast(on, salvar = true) {
  document.body.classList.toggle("contrast", on);
  $("#accessContrast").checked = on;
  localStorage.setItem("guiaContrast", on ? "1" : "0");
  if (salvar) salvarConfig({ altoContraste: on });
}
function renderFont(salvar = true) {
  document.documentElement.style.setProperty("--font-scale", fontScale);
  $("#fontValue").textContent = Math.round(fontScale * 100) + "%";
  localStorage.setItem("guiaFontScale", fontScale);
  if (salvar) salvarConfig({ fontePercentual: Math.round(fontScale * 100) });
}
function aplicarAcessibilidade(cfg) {
  setDark(!!cfg.modoEscuro, false);
  setContrast(!!cfg.altoContraste, false);
  fontScale = Math.min(1.25, Math.max(0.9, (cfg.fontePercentual || 100) / 100));
  renderFont(false);
}

$("#darkBtn").addEventListener("click", () => setDark(!document.body.classList.contains("dark")));
$("#contrastBtn").addEventListener("click", () => setContrast(!document.body.classList.contains("contrast")));
$("#accessDark").addEventListener("change", (e) => setDark(e.target.checked));
$("#accessContrast").addEventListener("change", (e) => setContrast(e.target.checked));
$("#fontUp").addEventListener("click", () => { fontScale = Math.min(1.25, +(fontScale + 0.05).toFixed(2)); renderFont(); });
$("#fontDown").addEventListener("click", () => { fontScale = Math.max(0.9, +(fontScale - 0.05).toFixed(2)); renderFont(); });
renderFont(false);
setDark(localStorage.getItem("guiaDark") === "1", false);
setContrast(localStorage.getItem("guiaContrast") === "1", false);

// ---------------------------------------------------------------- intervenção ("Hora de uma pausa!")

const TEXTOS_INTERVENCAO = {
  tempo_sessao: "Você já está usando o dispositivo pelo tempo configurado. Que tal fazer um desafio rápido de alfabetização?",
  limite_diario: "O tempo de tela de hoje acabou. Que tal fechar com um desafio rápido de alfabetização?",
  fora_do_horario: "Agora está fora do horário combinado para usar o dispositivo. Que tal um desafio rápido antes de parar?"
};

function openModal(motivo) {
  $("#interventionModal p").textContent = TEXTOS_INTERVENCAO[motivo] || TEXTOS_INTERVENCAO.tempo_sessao;
  $("#interventionModal").classList.remove("hidden");
}
function closeModal() { $("#interventionModal").classList.add("hidden"); }

function iniciarMonitor() {
  if (state.pararMonitor) state.pararMonitor();
  state.pararMonitor = API.monitorarUso((uso) => {
    // Se a criança já está fazendo um desafio, não interrompe.
    const emDesafio = $("#page-desafios").classList.contains("active") && !$("#challengeArea").classList.contains("hidden");
    if (emDesafio) return;
    if (state.crianca && state.crianca.configuracao.bloqueioSimulado) openModal(uso.motivoIntervencao);
    else toast("Aviso: " + (TEXTOS_INTERVENCAO[uso.motivoIntervencao] || "hora de uma pausa.").split(".")[0] + ".");
  }, { intervaloSeg: 30, onAtualizar: renderUso });
}

$("#simulateIntervention").addEventListener("click", () => {
  if (API.sessaoId()) API.registrarIntervencao().catch(() => {});
  openModal("tempo_sessao");
});

$("#modalClose").addEventListener("click", async () => {
  closeModal();
  try { await API.encerrarSessao(); } catch (e) { /* ignora */ }
  try { renderUso(await API.usoHoje()); } catch (e) { /* ignora */ }
  toast("Sessão encerrada.");
});

$("#modalChallenge").addEventListener("click", () => {
  closeModal();
  showPage("desafios");
  iniciarDesafio();
});

// ---------------------------------------------------------------- microdesafios

async function iniciarDesafio() {
  const area = $("#challengeArea");
  area.classList.remove("hidden");
  $("#challengeResult").classList.add("hidden");
  $("#nextQuestion").classList.add("hidden");
  $("#answersBox").innerHTML = "";
  setText("#challengeQuestion", "Carregando…");
  area.scrollIntoView({ behavior: "smooth", block: "center" });

  try {
    if (!API.sessaoId()) await API.iniciarSessao();
    if (!state.desafioPalavrasId) {
      const desafios = await API.desafios();
      const palavras = desafios.find((d) => d.tipo === "Palavras" && d.disponivel) || desafios.find((d) => d.disponivel);
      if (!palavras) throw new Error("Nenhum desafio disponível.");
      state.desafioPalavrasId = palavras.id;
    }
    state.perguntas = await API.perguntas(state.desafioPalavrasId, 5);
    state.indice = 0;
    state.acertos = 0;
    mostrarPergunta();
  } catch (e) {
    setText("#challengeQuestion", "Não foi possível carregar o desafio.");
    toast(mensagemErro(e));
  }
}

function mostrarPergunta() {
  const p = state.perguntas[state.indice];
  setText("#challengeTag", `Desafio ${state.indice + 1} de ${state.perguntas.length} · Palavras`);
  // Deixa a letra pedida em destaque, como no layout original.
  $("#challengeQuestion").innerHTML = escapeHtml(p.enunciado).replace(/letra (\S)\?$/, "letra <strong>$1</strong>?");
  $("#answersBox").innerHTML = p.opcoes.map((o) =>
    `<button type="button" data-opcao="${o.id}">${o.emoji ? escapeHtml(o.emoji) + " " : ""}${escapeHtml(o.texto)}</button>`
  ).join("");
  $("#challengeResult").classList.add("hidden");
  $("#nextQuestion").classList.add("hidden");
  state.exibidaEm = performance.now();
  state.primeiraTentativa = true;
}

$("#answersBox").addEventListener("click", async (e) => {
  const btn = e.target.closest("button[data-opcao]");
  if (!btn || btn.disabled) return;
  const p = state.perguntas[state.indice];
  const botoes = $$("#answersBox button");
  botoes.forEach((b) => (b.disabled = true));

  try {
    const r = await API.responder({
      perguntaId: p.id,
      opcaoId: Number(btn.dataset.opcao),
      tempoRespostaMs: performance.now() - state.exibidaEm
    });
    state.crianca.pontos = r.pontosTotal;
    setText("#pointsLabel", `⭐ ${r.pontosTotal} pontos`);

    const result = $("#challengeResult");
    result.classList.remove("hidden");
    if (r.acertou) {
      if (state.primeiraTentativa) state.acertos++;
      btn.classList.add("certa");
      result.className = "result-box success";
      result.textContent = `🎉 Muito bem! Resposta correta. +${r.pontosGanhos} pontos.`;
      const ultima = state.indice >= state.perguntas.length - 1;
      $("#nextQuestion").textContent = ultima ? "Concluir desafio ✓" : "Próxima pergunta →";
      $("#nextQuestion").classList.remove("hidden");
      toast("Desafio concluído!");
    } else {
      state.primeiraTentativa = false;
      btn.classList.add("errada");
      result.className = "result-box error";
      const letra = (p.enunciado.match(/letra (\S)\?$/) || [])[1];
      result.textContent = `💡 Quase! Vamos tentar outra vez.${letra ? ` Observe a letra ${letra}.` : ""}`;
      botoes.forEach((b) => { if (!b.classList.contains("errada")) b.disabled = false; });
      toast("Tente novamente.");
    }
  } catch (err) {
    botoes.forEach((b) => (b.disabled = false));
    toast(mensagemErro(err));
  }
});

$("#nextQuestion").addEventListener("click", () => {
  if (state.indice < state.perguntas.length - 1) {
    state.indice++;
    mostrarPergunta();
    return;
  }
  const total = state.perguntas.length;
  $("#answersBox").innerHTML = "";
  $("#nextQuestion").classList.add("hidden");
  setText("#challengeTag", "Desafio concluído · Palavras");
  setText("#challengeQuestion", `Você acertou ${state.acertos} de ${total} de primeira! ⭐`);
  const result = $("#challengeResult");
  result.className = "result-box success";
  result.textContent = `Total de pontos: ${state.crianca.pontos}. Clique em "Começar" para jogar de novo.`;
  carregarPainel();
});

$("#startChallenge").addEventListener("click", iniciarDesafio);

// ---------------------------------------------------------------- início

(async function iniciar() {
  if (!API.isLogged()) { mostrarLogin(); return; }
  try {
    await entrar(await API.me());
  } catch (e) {
    API.logout();
    mostrarLogin();
    if (e instanceof API.ApiError && e.status === 0) toast(mensagemErro(e));
  }
})();
