const PLAYER_COLORS = [
  '#e74c3c', // Rød
  '#3498db', // Blå
  '#f1c40f', // Gul
  '#2ecc71', // Grøn
  '#9b59b6', // Lilla
  '#e67e22'  // Orange
];

const GROUP_COLORS = {
  Blue: '#2b70c9',
  Orange: '#e67e22',
  Green: '#27ae60',
  Grey: '#7f8c8d',
  Red: '#c0392b',
  White: '#bdc3c7',
  Yellow: '#f1c40f',
  Purple: '#8e44ad',
  Shipping: '#2980b9',
  Brewery: '#d35400'
};

const DICE_FACES = ['⚀', '⚁', '⚂', '⚃', '⚄', '⚅'];

const params = new URLSearchParams(window.location.search);
if (params.get('room')) {
  localStorage.setItem('matador_room', params.get('room'));
  if (params.get('token')) localStorage.setItem('matador_token', params.get('token'));
  if (params.get('playerId')) localStorage.setItem('matador_playerId', params.get('playerId'));
  if (params.get('name')) localStorage.setItem('matador_name', params.get('name'));
  if (params.get('isHost')) localStorage.setItem('matador_isHost', params.get('isHost'));
}

// Tilstand for den lokale enhed
let mySession = {
  roomCode: localStorage.getItem('matador_room') || null,
  playerId: localStorage.getItem('matador_playerId') || null,
  token: localStorage.getItem('matador_token') || null,
  isHost: localStorage.getItem('matador_isHost') === 'true',
  name: localStorage.getItem('matador_name') || '',
  tokenIcon: localStorage.getItem('matador_icon') || '🎩'
};

const CORNER_ICONS = {
  0: { icon: '🚩', text: 'START (+4.000)' },
  10: { icon: '⚖️', text: 'FÆNGSEL / BESØG' },
  20: { icon: '🚗', text: 'PARKERING' },
  30: { icon: '👮', text: 'GÅ I FÆNGSEL' }
};

let currentGameState = null;
let selectedSpaceIndex = null;
let isAnimating = false;
let pollingTimer = null;

// Grid positionering af felterne
function getGridPosition(index) {
  if (index >= 0 && index <= 10) return { row: 11, col: 11 - index };
  if (index >= 11 && index <= 20) return { row: 11 - (index - 10), col: 1 };
  if (index >= 21 && index <= 30) return { row: 1, col: 1 + (index - 20) };
  return { row: 1 + (index - 30), col: 11 };
}

/* ============================================================
   LOBBY & LOGIN LOGIK
   ============================================================ */
const lobbyModal = document.getElementById('lobby-modal');
const gameView = document.getElementById('game-view');
const waitingRoom = document.getElementById('waiting-room');
const displayRoomCode = document.getElementById('display-room-code');
const joinedPlayersList = document.getElementById('joined-players-list');
const btnStartGame = document.getElementById('btn-start-game');

document.getElementById('btn-create-room').addEventListener('click', async () => {
  const name = document.getElementById('player-name-input').value.trim();
  const city = document.getElementById('city-select').value;
  const icon = document.getElementById('player-token-select').value;
  if (!name) return alert('Indtast venligst dit spillernavn.');

  const res = await fetch('/api/rooms/create', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ playerName: name, city: city })
  });

  if (!res.ok) return alert('Kunne ikke oprette rum.');
  const data = await res.json();
  saveSession(data.roomCode, data.playerId, data.token, true, name, icon);
  startPolling();
});

document.getElementById('btn-join-room').addEventListener('click', async () => {
  const name = document.getElementById('player-name-input').value.trim();
  const code = document.getElementById('join-code-input').value.trim().toUpperCase();
  const icon = document.getElementById('player-token-select').value;
  if (!name || !code) return alert('Indtast venligst både dit navn og rumkoden.');

  const res = await fetch('/api/rooms/join', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ roomCode: code, playerName: name })
  });

  if (!res.ok) {
    const err = await res.text();
    return alert(`Fejl: ${err}`);
  }

  const data = await res.json();
  saveSession(data.roomCode, data.playerId, data.token, false, name, icon);
  startPolling();
});

function saveSession(roomCode, playerId, token, isHost, name, icon) {
  mySession = { roomCode, playerId, token, isHost, name, tokenIcon: icon };
  localStorage.setItem('matador_room', roomCode);
  localStorage.setItem('matador_playerId', playerId);
  localStorage.setItem('matador_token', token);
  localStorage.setItem('matador_isHost', isHost ? 'true' : 'false');
  localStorage.setItem('matador_name', name);
  localStorage.setItem('matador_icon', icon);
}

document.getElementById('btn-close-room').addEventListener('click', async () => {
  if (confirm('Vil du lukke og annullere dette spilrum?')) {
    if (mySession.roomCode && mySession.token) {
      await fetch(`/api/rooms/${mySession.roomCode}/close`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: mySession.token })
      });
    }
    clearSession();
    location.reload();
  }
});

btnStartGame.addEventListener('click', async () => {
  if (!mySession.roomCode || !mySession.token) return;

  const res = await fetch(`/api/rooms/${mySession.roomCode}/start`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });

  if (!res.ok) {
    const err = await res.text();
    alert(`Fejl ved start: ${err}`);
  } else {
    fetchGameState();
  }
});

document.getElementById('btn-leave-room').addEventListener('click', async () => {
  if (confirm('Er du sikker på, at du vil forlade spillet? (Du opgiver hermed, og modspilleren vinder!)')) {
    if (mySession.roomCode && mySession.token) {
      await fetch(`/api/rooms/${mySession.roomCode}/leave`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: mySession.token })
      });
    }
    clearSession();
    location.reload();
  }
});

function saveSession(roomCode, playerId, token, isHost, name) {
  mySession = { roomCode, playerId, token, isHost, name };
  localStorage.setItem('matador_room', roomCode);
  localStorage.setItem('matador_playerId', playerId);
  localStorage.setItem('matador_token', token);
  localStorage.setItem('matador_isHost', isHost ? 'true' : 'false');
  localStorage.setItem('matador_name', name);
}

function clearSession() {
  localStorage.removeItem('matador_room');
  localStorage.removeItem('matador_playerId');
  localStorage.removeItem('matador_token');
  localStorage.removeItem('matador_isHost');
  localStorage.removeItem('matador_name');
  mySession = { roomCode: null, playerId: null, token: null, isHost: false, name: '' };
}

/* ============================================================
   REAL-TIME SYNKRONISERING & POLLING
   ============================================================ */
function startPolling() {
  if (pollingTimer) clearInterval(pollingTimer);
  fetchGameState();
  pollingTimer = setInterval(fetchGameState, 1000); // Tjekker server hvert sekund
}

async function fetchGameState() {
  if (!mySession.roomCode) return;

  try {
    const res = await fetch(`/api/rooms/${mySession.roomCode}/state?token=${mySession.token}`);
    if (res.status === 404) {
      clearSession();
      alert('Spilrummet blev ikke fundet eller er udløbet.');
      location.reload();
      return;
    }
    if (!res.ok) return;

    const data = await res.json();

    // Tjek om vi er i lobby eller i selve spillet
    if (!data.isStarted) {
      renderLobbyWaiting(data);
    } else {
      lobbyModal.style.display = 'none';
      gameView.style.display = 'flex';
      currentGameState = data;
      renderBoard();
      renderUI();
    }
  } catch (err) {
    console.error('Netværksfejl under hentning af spil:', err);
  }
}

const btnAddBot = document.getElementById('btn-add-bot');

if (btnAddBot) {
  btnAddBot.addEventListener('click', async () => {
    if (!mySession.roomCode || !mySession.token) return;
    const res = await fetch(`/api/rooms/${mySession.roomCode}/addbot`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: mySession.token })
    });
    if (!res.ok) {
      const err = await res.text();
      alert(`Kunne ikke tilføje bot: ${err}`);
    } else {
      fetchGameState();
    }
  });
}

window.kickLobbyPlayer = async function(playerId) {
  if (!mySession.roomCode || !mySession.token) return;
  await fetch(`/api/rooms/${mySession.roomCode}/kickplayer`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token, playerId })
  });
  fetchGameState();
};

function renderLobbyWaiting(data) {
  lobbyModal.style.display = 'flex';
  gameView.style.display = 'none';
  waitingRoom.classList.remove('hidden');
  document.querySelector('.lobby-split').classList.add('hidden');
  displayRoomCode.innerText = data.roomCode;

  joinedPlayersList.innerHTML = '';
  data.lobbyPlayers.forEach(p => {
    const div = document.createElement('div');
    div.className = 'joined-player-item';
    
    let kickBtn = '';
    if (mySession.isHost && p.playerId !== mySession.playerId) {
      kickBtn = `<button onclick="kickLobbyPlayer('${p.playerId}')" style="background: none; border: none; color: #da3633; cursor: pointer; font-size: 0.8rem; padding: 2px 6px;" title="Fjern">✕</button>`;
    }

    div.innerHTML = `
      <span><strong>${p.name}</strong> ${p.playerId === mySession.playerId ? '(Dig)' : ''} ${p.isBot ? '<span style="background: #1f6feb; color: #fff; padding: 1px 6px; border-radius: 8px; font-size: 0.7rem; margin-left: 4px;">AI Bot</span>' : ''}</span>
      <span>${p.isHost ? '👑 Vært' : 'Klar'} ${kickBtn}</span>
    `;
    joinedPlayersList.appendChild(div);
  });

  if (mySession.isHost) {
    if (btnAddBot) {
      btnAddBot.classList.remove('hidden');
      btnAddBot.disabled = data.lobbyPlayers.length >= 6;
    }
    btnStartGame.classList.remove('hidden');
    btnStartGame.disabled = data.lobbyPlayers.length < 2;
    btnStartGame.innerText = data.lobbyPlayers.length < 2 ? 'Venter på mindst 2 spillere...' : `Start Spil (${data.lobbyPlayers.length} spillere)`;
  } else {
    if (btnAddBot) btnAddBot.classList.add('hidden');
    btnStartGame.classList.add('hidden');
  }
}

/* ============================================================
   BRÆT- OG SPIL-RENDERING MED ASYMMETRISK SYNLIGHED
   ============================================================ */
function renderBoard(displayedPositions = null) {
  const boardEl = document.getElementById('board');
  if (!boardEl || !currentGameState) return;

  const existingSpaces = boardEl.querySelectorAll('.space');
  existingSpaces.forEach(el => el.remove());

  currentGameState.spaces.forEach(space => {
    const pos = getGridPosition(space.index);
    const spaceEl = document.createElement('div');
    spaceEl.className = 'space';
    spaceEl.id = `space-${space.index}`;
    // Hjørnefelter med flot grafik
    if ([0, 10, 20, 30].includes(space.index)) {
      spaceEl.classList.add('corner');
      const cornerData = CORNER_ICONS[space.index];
      spaceEl.innerHTML = `
        <div class="corner-icon">${cornerData.icon}</div>
        <div class="space-title">${cornerData.text}</div>
      `;
    } else {
      // Almindeligt felt
      if (space.group && GROUP_COLORS[space.group]) {
        const bar = document.createElement('div');
        bar.className = 'space-color-bar';
        bar.style.backgroundColor = GROUP_COLORS[space.group];
        spaceEl.appendChild(bar);
      }

      const title = document.createElement('div');
      title.className = 'space-title';
      title.innerText = space.name;
      spaceEl.appendChild(title);

      if (space.price > 0 && !space.ownerName) {
        const price = document.createElement('div');
        price.className = 'space-price';
        price.innerText = `kr. ${space.price.toLocaleString('da-DK')}`;
        spaceEl.appendChild(price);
      }
    }

    if (space.ownerId) {
      const ownerIdx = currentGameState.players.findIndex(p => p.id === space.ownerId);
      if (ownerIdx !== -1) {
        const ownerInd = document.createElement('div');
        ownerInd.className = 'owner-indicator';
        ownerInd.style.borderRightColor = PLAYER_COLORS[ownerIdx % PLAYER_COLORS.length];
        ownerInd.style.borderTopColor = 'transparent';
        spaceEl.appendChild(ownerInd);
      }
    }

    // Huse og Hoteller som rigtig grafik
    if (space.houseCount > 0) {
      const buildCont = document.createElement('div');
      buildCont.className = 'buildings-container';
      if (space.houseCount === 5) {
        const hotel = document.createElement('span');
        hotel.className = 'hotel-indicator';
        hotel.innerText = '🏨';
        buildCont.appendChild(hotel);
      } else {
        for (let h = 0; h < space.houseCount; h++) {
          const house = document.createElement('span');
          house.className = 'house-indicator';
          house.innerText = '🏠';
          buildCont.appendChild(house);
        }
      }
      spaceEl.appendChild(buildCont);
    }

    // Brikker med valgte brik-ikoner (f.eks. 🎩, 🚗, 🐕, ⛵, 🚲, 🍺)
    const tokensCont = document.createElement('div');
    tokensCont.className = 'tokens-container';
    currentGameState.players.forEach((p, idx) => {
      const currentPos = displayedPositions ? displayedPositions[p.id] : p.position;
      if (currentPos === space.index && !p.isBankrupt) {
        const tok = document.createElement('span');
        tok.className = 'token';
        tok.id = `token-${p.id}`;
        // Standard ikoner baseret på spillerens index hvis ikke sat
        const icons = ['🎩', '🚗', '🐕', '⛵', '🚲', '🍺'];
        tok.innerText = p.id === mySession.playerId ? (mySession.tokenIcon || icons[idx % icons.length]) : icons[idx % icons.length];
        tok.title = p.name;
        tokensCont.appendChild(tok);
      }
    });
    spaceEl.appendChild(tokensCont);

    spaceEl.addEventListener('click', () => {
      selectedSpaceIndex = space.index;
      renderSelectedSpace();
    });

    boardEl.appendChild(spaceEl);
  });
}

function renderUI() {
  if (!currentGameState) return;

  document.getElementById('top-room-badge').innerText = `Rum: ${currentGameState.roomCode}`;
  const myPlayer = currentGameState.players.find(p => p.id === currentGameState.myPlayerId);
  const myBadge = document.getElementById('my-identity-badge');
  if (myPlayer) {
    myBadge.innerText = `Du er: ${myPlayer.name} (kr. ${myPlayer.balance.toLocaleString('da-DK')})`;
  }

  // Hvem har turen?
  const curP = currentGameState.currentPlayer;
  const isMyTurn = currentGameState.isMyTurn;
  const pNameEl = document.getElementById('turn-player-name');
  if (pNameEl) {
    pNameEl.innerText = isMyTurn ? `🎯 DIN TUR (${curP.name})` : `${curP.name} (kr. ${curP.balance.toLocaleString('da-DK')})`;
    pNameEl.style.color = isMyTurn ? 'var(--accent-gold)' : '#fff';
  }

  // Puljen (JackpotPool)
  const poolEl = document.getElementById('jackpot-amount');
  if (poolEl && currentGameState.jackpotPool !== undefined) {
    poolEl.innerText = `kr. ${currentGameState.jackpotPool.toLocaleString('da-DK')}`;
  }

  // Tjek om spillet er slut (f.eks. modspiller forlod eller gik bankerot)
  if (currentGameState.isGameOver) {
    pNameEl.innerText = `🏆 SPILLET ER SLUT!`;
    pNameEl.style.color = 'var(--accent-gold)';
    turnWaitMsg.classList.remove('hidden');
    turnWaitMsg.innerHTML = `<span style="font-size: 1.1rem; font-weight: 700;">🎉 Vinderen er: ${currentGameState.winner || 'Uafgjort'}!</span><br><small style="color: #8b949e;">En spiller har forladt spillet eller er gået bankerot.</small>`;
    btnRoll.classList.add('hidden');
    buyActions.classList.add('hidden');
    btnEndTurn.classList.add('hidden');
    btnJailPay.classList.add('hidden');
    if (btnClaim) btnClaim.classList.add('hidden');
    return;
  }

  // Håndtering af Aktiv Afstemning (når en spiller har forladt spillet)
  const voteModal = document.getElementById('vote-modal');
  const voteDesc = document.getElementById('vote-modal-desc');
  const voteStatus = document.getElementById('vote-status-text');
  const voteActions = document.getElementById('vote-actions');

  if (currentGameState.activeVote) {
    voteModal.classList.remove('hidden');
    voteDesc.innerText = `${currentGameState.activeVote.leavingPlayerName} har forladt spillet! Skal spillet fortsætte mellem jer, eller skal det stoppe og en vinder kåres?`;
    voteStatus.innerText = `Afgivne stemmer: ${currentGameState.activeVote.votesCount} ud af ${currentGameState.activeVote.totalEligible}`;

    if (currentGameState.activeVote.hasVoted) {
      voteActions.innerHTML = `<span style="color: var(--accent-gold); font-weight: 600;">✓ Du har stemt! Afventer andre spillere...</span>`;
    } else {
      voteActions.innerHTML = `
        <button id="btn-vote-continue" class="btn-success" style="padding: 10px 18px;" onclick="castVote(true)">Fortsæt Spillet</button>
        <button id="btn-vote-stop" class="btn-danger" style="padding: 10px 18px;" onclick="castVote(false)">Stop Spillet</button>
      `;
    }
  } else {
    voteModal.classList.add('hidden');
  }

  // Kontrolknapper: KUN aktive på din enhed når det er DIN tur!
  const phase = currentGameState.phase;
  const btnRoll = document.getElementById('btn-roll');
  const buyActions = document.getElementById('buy-actions');
  const btnEndTurn = document.getElementById('btn-end-turn');
  const btnJailPay = document.getElementById('btn-jail-pay');
  const turnWaitMsg = document.getElementById('turn-wait-msg');

  btnRoll.classList.add('hidden');
  buyActions.classList.add('hidden');
  btnEndTurn.classList.add('hidden');
  btnJailPay.classList.add('hidden');
  turnWaitMsg.classList.add('hidden');

  if (isMyTurn) {
    if (curP.isInJail && phase === 'WaitingForRoll') {
      btnJailPay.classList.remove('hidden');
    }

    if (phase === 'WaitingForRoll') {
      btnRoll.classList.remove('hidden');
    } else if (phase === 'PendingBuyOrPass') {
      buyActions.classList.remove('hidden');
    } else if (phase === 'ActionResolved') {
      btnEndTurn.classList.remove('hidden');
    }
  } else {
    turnWaitMsg.classList.remove('hidden');
    turnWaitMsg.innerText = `Afventer ${curP.name}...`;
  }

  // Seneste hændelse
  if (currentGameState.logs.length > 0) {
    document.getElementById('last-event-box').innerText = currentGameState.logs[currentGameState.logs.length - 1];
  }

  // Spillerliste
  const pListEl = document.getElementById('players-list');
  pListEl.innerHTML = '';
  currentGameState.players.forEach((p, idx) => {
    const item = document.createElement('div');
    item.className = 'player-item';
    if (idx === currentGameState.currentPlayerIndex) item.classList.add('active');

    let jailBadge = '';
    if (p.getOutOfJailCards > 0) {
      jailBadge = `<span style="background: #8e44ad; color: #fff; padding: 1px 6px; border-radius: 10px; font-size: 0.68rem; margin-left: 4px;">🎟️ ${p.getOutOfJailCards} Frikort</span>`;
    }

    let botBadge = '';
    if (p.isBot) {
      botBadge = `<span style="background: #1f6feb; color: #fff; padding: 1px 6px; border-radius: 8px; font-size: 0.68rem; margin-left: 4px;">AI Bot</span>`;
    }

    item.innerHTML = `
      <div class="player-info">
        <div class="player-token-badge" style="background-color: ${PLAYER_COLORS[idx % PLAYER_COLORS.length]}"></div>
        <div>
          <div class="player-name">${p.name} ${p.id === currentGameState.myPlayerId ? '(Dig)' : ''} ${botBadge} ${p.isBankrupt ? '(Bankerot)' : ''} ${jailBadge}</div>
          <div style="font-size: 0.75rem; color: #8b949e;">Ejendomme: ${p.ownedCount} | Formue: kr. ${p.netWorth.toLocaleString('da-DK')}</div>
        </div>
      </div>
      <div class="player-balance">kr. ${p.balance.toLocaleString('da-DK')}</div>
    `;
    pListEl.appendChild(item);
  });

  renderTrades();

  // Log-stream
  const logStream = document.getElementById('log-stream');
  logStream.innerHTML = '';
  [...currentGameState.logs].reverse().forEach(log => {
    const entry = document.createElement('div');
    entry.className = 'log-entry';
    entry.innerText = log;
    logStream.appendChild(entry);
  });

  renderSelectedSpace();
}

function renderTrades() {
  const tradesContainer = document.getElementById('trades-list');
  if (!tradesContainer || !currentGameState) return;

  if (!currentGameState.pendingTrades || currentGameState.pendingTrades.length === 0) {
    tradesContainer.innerHTML = '<p class="placeholder-text">Ingen aktive handelstilbud til dig.</p>';
    return;
  }

  tradesContainer.innerHTML = '';
  currentGameState.pendingTrades.forEach(t => {
    const div = document.createElement('div');
    div.className = 'trade-offer-item';
    div.innerHTML = `
      <div><strong>${t.fromPlayer}</strong> vil købe <strong>${t.propertyName}</strong> af dig for <strong>kr. ${t.price.toLocaleString('da-DK')}</strong>.</div>
      <div class="trade-actions">
        <button class="btn-success" style="padding: 4px 10px; font-size: 0.75rem;" onclick="respondTrade(${t.id}, true)">Accepter</button>
        <button class="btn-danger" style="padding: 4px 10px; font-size: 0.75rem;" onclick="respondTrade(${t.id}, false)">Afvis</button>
      </div>
    `;
    tradesContainer.appendChild(div);
  });
}

window.castVote = async function(continueGame) {
  await fetch(`/api/rooms/${mySession.roomCode}/vote`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token, continueGame: continueGame })
  });
  fetchGameState();
};

window.respondTrade = async function(tradeId, accept) {
  await fetch(`/api/rooms/${mySession.roomCode}/trade/respond`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token, tradeId, accept })
  });
  fetchGameState();
};

function renderSelectedSpace() {
  if (selectedSpaceIndex === null || !currentGameState) return;

  const space = currentGameState.spaces.find(s => s.index === selectedSpaceIndex);
  if (!space) return;

  document.getElementById('selected-title').innerText = space.name;
  const detailsEl = document.getElementById('selected-details');

  let html = `<p><strong>Felt:</strong> #${space.index} (${space.type})</p>`;
  if (space.price > 0) {
    html += `<p><strong>Købspris:</strong> kr. ${space.price.toLocaleString('da-DK')}</p>`;
    html += `<p><strong>Ejer:</strong> ${space.ownerName || 'Ingen (Ledig)'}</p>`;
    html += `<p><strong>Nuværende Leje:</strong> kr. ${space.currentRent.toLocaleString('da-DK')}</p>`;

    if (space.housePrice > 0) {
      html += `<p><strong>Pris pr. hus:</strong> kr. ${space.housePrice.toLocaleString('da-DK')}</p>`;
      html += `<p><strong>Huse på grunden:</strong> ${space.houseCount === 5 ? 'Hotel' : space.houseCount}</p>`;

      const isOwner = space.ownerId === currentGameState.myPlayerId;
      if (isOwner && space.houseCount < 5) {
        html += `<button id="btn-build-house" class="btn-success" style="margin-top: 8px; width: 100%;">Byg Hus / Hotel</button>`;
      }
    }

    // Byd på grunden hvis den ejes af en modspiller
    if (space.ownerId && space.ownerId !== currentGameState.myPlayerId) {
      html += `<button id="btn-quick-bid" class="btn-primary" style="margin-top: 8px; width: 100%;">Byd på denne grund...</button>`;
    }
  }

  detailsEl.innerHTML = html;

  const btnBuild = document.getElementById('btn-build-house');
  if (btnBuild) {
    btnBuild.addEventListener('click', async () => {
      await fetch(`/api/rooms/${mySession.roomCode}/buyhouse/${space.index}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: mySession.token })
      });
      fetchGameState();
    });
  }

  const btnQuickBid = document.getElementById('btn-quick-bid');
  if (btnQuickBid) {
    btnQuickBid.addEventListener('click', () => {
      openTradeModalWithProperty(space.index, space.ownerId);
    });
  }
}

/* ============================================================
   ANIMATIONER & INTERAKTIONER
   ============================================================ */
async function animateDiceRoll() {
  const die1 = document.getElementById('die1');
  const die2 = document.getElementById('die2');
  die1.classList.add('rolling');
  die2.classList.add('rolling');

  for (let i = 0; i < 6; i++) {
    die1.innerText = DICE_FACES[Math.floor(Math.random() * 6)];
    die2.innerText = DICE_FACES[Math.floor(Math.random() * 6)];
    await new Promise(r => setTimeout(r, 70));
  }

  die1.classList.remove('rolling');
  die2.classList.remove('rolling');
}

async function animatePlayerMovement(playerId, startPos, targetPos) {
  let steps = targetPos - startPos;
  if (steps < 0) steps += 40;

  const positions = {};
  currentGameState.players.forEach(p => { positions[p.id] = p.position; });

  let cur = startPos;
  for (let s = 1; s <= steps; s++) {
    cur = (cur + 1) % 40;
    positions[playerId] = cur;
    renderBoard(positions);

    if (cur === 0) {
      const startEl = document.getElementById('space-0');
      if (startEl) startEl.classList.add('highlight-pass');
    }

    await new Promise(r => setTimeout(r, 140));
  }
}

document.getElementById('btn-roll').addEventListener('click', async () => {
  if (isAnimating) return;
  isAnimating = true;

  const oldPlayer = currentGameState.currentPlayer;
  const startPos = oldPlayer.position;

  await animateDiceRoll();

  await fetch(`/api/rooms/${mySession.roomCode}/roll`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });

  const res = await fetch(`/api/rooms/${mySession.roomCode}/state?token=${mySession.token}`);
  const nextState = await res.json();
  const updatedPlayer = nextState.players.find(p => p.id === oldPlayer.id);

  if (updatedPlayer && !updatedPlayer.isInJail) {
    await animatePlayerMovement(oldPlayer.id, startPos, updatedPlayer.position);
  }

  currentGameState = nextState;
  isAnimating = false;
  renderBoard();
  renderUI();
});

document.getElementById('btn-claim-rent').addEventListener('click', async () => {
  await fetch(`/api/rooms/${mySession.roomCode}/claimrent`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });
  fetchGameState();
});

document.getElementById('btn-buy').addEventListener('click', async () => {
  await fetch(`/api/rooms/${mySession.roomCode}/buy`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });
  fetchGameState();
});

document.getElementById('btn-pass').addEventListener('click', async () => {
  await fetch(`/api/rooms/${mySession.roomCode}/pass`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });
  fetchGameState();
});

document.getElementById('btn-end-turn').addEventListener('click', async () => {
  await fetch(`/api/rooms/${mySession.roomCode}/endturn`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });
  fetchGameState();
});

document.getElementById('btn-jail-pay').addEventListener('click', async () => {
  await fetch(`/api/rooms/${mySession.roomCode}/payjail`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });
  fetchGameState();
});

/* ============================================================
   BYGGERI: HUSE & HOTELLER
   ============================================================ */
const buildModal = document.getElementById('build-modal');
const buildPropsList = document.getElementById('build-properties-list');

document.getElementById('btn-open-build').addEventListener('click', () => {
  openBuildModal();
});

document.getElementById('btn-close-build').addEventListener('click', () => {
  buildModal.classList.add('hidden');
});

function openBuildModal() {
  if (!currentGameState) return;
  buildPropsList.innerHTML = '';

  const myPlayerId = currentGameState.myPlayerId;
  const myStreets = currentGameState.spaces.filter(s => s.ownerId === myPlayerId && s.housePrice > 0);

  if (myStreets.length === 0) {
    buildPropsList.innerHTML = '<p class="placeholder-text">Du ejer endnu ingen gader, der kan bygges på.</p>';
    buildModal.classList.remove('hidden');
    return;
  }

  // Gruppér gader efter farvegruppe for at tjekke monopol
  const groups = {};
  currentGameState.spaces.filter(s => s.housePrice > 0).forEach(s => {
    if (!groups[s.group]) groups[s.group] = [];
    groups[s.group].push(s);
  });

  let anyMonopoly = false;

  Object.keys(groups).forEach(grpKey => {
    const allInGrp = groups[grpKey];
    const myInGrp = allInGrp.filter(s => s.ownerId === myPlayerId);
    const hasMonopoly = myInGrp.length === allInGrp.length;

    if (myInGrp.length > 0) {
      const groupDiv = document.createElement('div');
      groupDiv.style.background = '#1c222b';
      groupDiv.style.padding = '8px 12px';
      groupDiv.style.borderRadius = '6px';
      groupDiv.style.borderLeft = `4px solid ${GROUP_COLORS[grpKey] || '#fff'}`;

      let grpStatus = hasMonopoly 
        ? `<span style="color: #3fb950; font-weight: 700; font-size: 0.75rem;">✓ Komplet serie (Byggeri tilladt!)</span>`
        : `<span style="color: #8b949e; font-size: 0.75rem;">Mangler grunde (${myInGrp.length}/${allInGrp.length} ejet)</span>`;

      let streetsHtml = `<div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
        <strong>${grpKey} gruppe</strong> ${grpStatus}
      </div>`;

      streetsHtml += '<div style="display: flex; flex-direction: column; gap: 6px;">';
      myInGrp.forEach(st => {
        const canBuild = hasMonopoly && st.houseCount < 5;
        const currentBuilding = st.houseCount === 5 ? '🏨 Hotel' : (st.houseCount > 0 ? `🏠 ${st.houseCount} hus(e)` : 'Ubebygget');
        const nextCost = st.housePrice;

        streetsHtml += `
          <div style="display: flex; justify-content: space-between; align-items: center; background: #252d3a; padding: 6px 10px; border-radius: 4px; font-size: 0.8rem;">
            <div>
              <strong>${st.name}</strong> - <span>${currentBuilding}</span>
              <div style="font-size: 0.72rem; color: #8b949e;">Leje nu: kr. ${st.currentRent.toLocaleString('da-DK')} | Huspris: kr. ${nextCost.toLocaleString('da-DK')}</div>
            </div>
            <div>
              ${canBuild 
                ? `<button class="btn-success" style="padding: 4px 10px; font-size: 0.75rem;" onclick="buyHouseFromModal(${st.index})">+ Byg (${nextCost >= 1000 ? (nextCost/1000)+'k' : nextCost})</button>`
                : `<span style="font-size: 0.7rem; color: #8b949e;">${st.houseCount === 5 ? 'Maks (Hotel)' : 'Kræver serie'}</span>`}
            </div>
          </div>
        `;
      });
      streetsHtml += '</div>';

      groupDiv.innerHTML = streetsHtml;
      buildPropsList.appendChild(groupDiv);

      if (hasMonopoly) anyMonopoly = true;
    }
  });

  buildModal.classList.remove('hidden');
}

window.buyHouseFromModal = async function(spaceIndex) {
  const res = await fetch(`/api/rooms/${mySession.roomCode}/buyhouse/${spaceIndex}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });

  if (res.ok) {
    await fetchGameState();
    openBuildModal(); // Opdater modal efter bygget
  } else {
    alert('Kunne ikke bygge på denne grund (tjek saldo eller regel om jævn bebyggelse).');
  }
};
const tradeModal = document.getElementById('trade-modal');
const sellerSelect = document.getElementById('trade-seller-select');
const propSelect = document.getElementById('trade-property-select');

document.getElementById('btn-open-trade').addEventListener('click', () => {
  if (!currentGameState) return;
  openTradeModal();
});

document.getElementById('btn-cancel-trade').addEventListener('click', () => {
  tradeModal.classList.add('hidden');
});

function openTradeModal() {
  const myP = currentGameState.players.find(p => p.id === currentGameState.myPlayerId);
  if (!myP) return;

  document.getElementById('trade-from-name').value = `${myP.name} (kr. ${myP.balance.toLocaleString('da-DK')})`;

  sellerSelect.innerHTML = '';
  currentGameState.players.filter(p => p.id !== myP.id && !p.isBankrupt).forEach(other => {
    const opt = document.createElement('option');
    opt.value = other.id;
    opt.innerText = `${other.name} (${other.ownedCount} ejendomme)`;
    sellerSelect.appendChild(opt);
  });

  updateSellerProperties();
  sellerSelect.onchange = updateSellerProperties;
  tradeModal.classList.remove('hidden');
}

function openTradeModalWithProperty(propIndex, sellerId) {
  openTradeModal();
  sellerSelect.value = sellerId;
  updateSellerProperties();
  propSelect.value = propIndex;
}

function updateSellerProperties() {
  const sellerId = sellerSelect.value;
  const seller = currentGameState.players.find(p => p.id === sellerId);
  propSelect.innerHTML = '';

  let hasItems = false;

  if (seller && seller.getOutOfJailCards > 0) {
    const opt = document.createElement('option');
    opt.value = "jail_card";
    opt.innerText = `🎟️ Fængsels-Frikort (${seller.getOutOfJailCards} stk haves)`;
    propSelect.appendChild(opt);
    hasItems = true;
  }

  if (seller && seller.ownedProperties && seller.ownedProperties.length > 0) {
    seller.ownedProperties.forEach(p => {
      const opt = document.createElement('option');
      opt.value = p.index;
      opt.innerText = `${p.name} (Pris: kr. ${p.price.toLocaleString('da-DK')})`;
      propSelect.appendChild(opt);
      hasItems = true;
    });
  }

  if (!hasItems) {
    const opt = document.createElement('option');
    opt.innerText = 'Ingen ejendomme eller frikort at sælge';
    opt.disabled = true;
    propSelect.appendChild(opt);
  }
}

document.getElementById('btn-send-trade').addEventListener('click', async () => {
  const toPlayerId = sellerSelect.value;
  const selectedVal = propSelect.value;
  const price = parseInt(document.getElementById('trade-price-input').value);

  if (isNaN(price) || price <= 0) {
    alert('Indtast et gyldigt beløb.');
    return;
  }

  const isJailCard = selectedVal === "jail_card";
  const propIndex = isJailCard ? -1 : parseInt(selectedVal);

  if (!isJailCard && isNaN(propIndex)) {
    alert('Vælg venligst en gyldig ejendom eller frikort.');
    return;
  }

  await fetch(`/api/rooms/${mySession.roomCode}/trade/propose`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      token: mySession.token,
      toPlayerId: toPlayerId,
      propertyIndex: propIndex,
      price: price,
      isJailCard: isJailCard
    })
  });

  tradeModal.classList.add('hidden');
  fetchGameState();
});

// Hvis vi allerede har en session gemt, start automatisk opkobling
if (mySession.roomCode && mySession.token) {
  startPolling();
}
