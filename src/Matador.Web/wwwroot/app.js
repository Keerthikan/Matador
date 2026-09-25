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

const GROUP_DANISH = {
  Blue: 'Blå',
  Orange: 'Orange',
  Green: 'Grøn',
  Grey: 'Grå',
  Red: 'Rød',
  White: 'Hvid',
  Yellow: 'Gul',
  Purple: 'Lilla',
  Shipping: 'Rederi ⛴️',
  Brewery: 'Bryggeri 🍺'
};

function getPropertyColorBadge(group) {
  if (!group) return '';
  const color = GROUP_COLORS[group] || '#888';
  const name = GROUP_DANISH[group] || group;
  return `<span style="background:${color}; color:#fff; padding:1px 6px; border-radius:4px; font-size:0.68rem; font-weight:700; text-shadow:0 1px 2px rgba(0,0,0,0.6); margin-right:4px;">${name}</span>`;
}

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
let lastRenderedStateJson = null;
let selectedSpaceIndex = null;
let isAnimating = false;
let pollingTimer = null;
let lastProcessedLogCount = 0;
let lastAlertedRentKey = null;
const knownPlayerPositions = {};

function showToast(message, type = 'info', icon = '🔔') {
  let container = document.getElementById('toast-container');
  if (!container) {
    container = document.createElement('div');
    container.id = 'toast-container';
    container.className = 'toast-container';
    document.body.appendChild(container);
  }

  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.innerHTML = `
    <span style="font-size: 1.2rem; line-height: 1;">${icon}</span>
    <div>${message}</div>
  `;

  container.appendChild(toast);

  setTimeout(() => {
    toast.classList.add('toast-fade-out');
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// Grid positionering af felterne (START er øverst til venstre, går med uret rundt)
function getGridPosition(index) {
  // Top-række: 0 (START top-left) til 10 (FÆNGSEL top-right) -> Row 1, Col 1..11
  if (index >= 0 && index <= 10) {
    return { row: 1, col: 1 + index };
  }
  // Højre kolonne: 11 til 20 (PARKERING bottom-right) -> Row 2..11, Col 11
  if (index >= 11 && index <= 20) {
    return { row: 1 + (index - 10), col: 11 };
  }
  // Bund-række: 21 til 30 (GÅ I FÆNGSEL bottom-left) -> Row 11, Col 10..1
  if (index >= 21 && index <= 30) {
    return { row: 11, col: 11 - (index - 20) };
  }
  // Venstre kolonne: 31 til 39 -> Row 10..2, Col 1
  return { row: 11 - (index - 30), col: 1 };
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
      
      const stateJson = JSON.stringify(data);
      const stateChanged = stateJson !== lastRenderedStateJson;
      currentGameState = data;

      // Tjek altid for nye hændelser og vis vigtige toasts straks (uanset om brættet genrendres)
      if (data.logs && data.logs.length > 0) {
        if (lastProcessedLogCount === 0) {
          lastProcessedLogCount = data.logs.length;
        } else if (data.logs.length > lastProcessedLogCount) {
          const newLogs = data.logs.slice(lastProcessedLogCount);
          lastProcessedLogCount = data.logs.length;

          newLogs.forEach(log => {
            const cleanLog = log.replace(/^[❌🤝💰🔔⚠️👀💼🎟️🏨🏠🏛️]\s*/, '');
            if (log.includes('HANDEL GENNEMFØRT')) {
              showToast(cleanLog, 'success', '🤝');
            } else if (log.toLowerCase().includes('afviste') || log.toLowerCase().includes('annulleret')) {
              showToast(cleanLog, 'danger', '❌');
            } else if (log.includes('JACKPOT')) {
              showToast(cleanLog, 'success', '💰');
            } else if (log.includes('opkrævede leje')) {
              showToast(cleanLog, 'info', '💸');
            } else if (log.includes('Fængsel') && !log.includes('På besøg')) {
              showToast(cleanLog, 'info', '👮');
            }
          });
        }
      }

      if (!isAnimating && stateChanged) {
        lastRenderedStateJson = stateJson;

        // Tjek om en spiller (f.eks. bot eller modspiller eller Prøv Lykken ryk) har skiftet position
        let playerMoved = null;
        for (const p of data.players) {
          if (knownPlayerPositions[p.id] !== undefined && knownPlayerPositions[p.id] !== p.position && !p.isBankrupt) {
            playerMoved = { id: p.id, from: knownPlayerPositions[p.id], to: p.position };
            break;
          }
        }

        // Opdater kendte positioner
        data.players.forEach(p => { knownPlayerPositions[p.id] = p.position; });

        if (playerMoved) {
          isAnimating = true;
          (async () => {
            await animatePlayerMovement(playerMoved.id, playerMoved.from, playerMoved.to);
            isAnimating = false;
            renderBoard();
            renderUI();
          })();
        } else {
          renderBoard();
          renderUI();
        }
      } else if (!isAnimating && !stateChanged) {
        data.players.forEach(p => { knownPlayerPositions[p.id] = p.position; });
      }
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
    spaceEl.style.gridRow = pos.row;
    spaceEl.style.gridColumn = pos.col;
    // Hjørnefelter med flot grafik
    if ([0, 10, 20, 30].includes(space.index)) {
      spaceEl.classList.add('corner');
      const cornerData = CORNER_ICONS[space.index];
      spaceEl.innerHTML = `
        <div class="corner-icon">${cornerData.icon}</div>
        <div class="space-title">${cornerData.text}</div>
      `;
    } else {
      // Almindeligt felt: gader har farvebjælke, rederier/transport og bryggerier har egne ikoner
      if (space.type === 'Street' && space.group && GROUP_COLORS[space.group]) {
        const bar = document.createElement('div');
        bar.className = 'space-color-bar';
        bar.style.backgroundColor = GROUP_COLORS[space.group];
        spaceEl.appendChild(bar);
      } else if (space.type === 'Shipping') {
        const shipIcon = document.createElement('div');
        shipIcon.className = 'special-space-icon';
        const nameLower = space.name.toLowerCase();
        if (nameLower.includes('bus')) {
          shipIcon.innerText = '🚌';
        } else if (nameLower.includes('letbane') || nameLower.includes('tog') || nameLower.includes('dsb')) {
          shipIcon.innerText = '🚊';
        } else {
          shipIcon.innerText = '⛴️';
        }
        shipIcon.title = 'Transport';
        spaceEl.appendChild(shipIcon);
      } else if (space.type === 'Brewery') {
        const brewIcon = document.createElement('div');
        brewIcon.className = 'special-space-icon';
        brewIcon.innerText = '🍺';
        brewIcon.title = 'Bryggeri';
        spaceEl.appendChild(brewIcon);
      }

      const title = document.createElement('div');
      title.className = 'space-title';
      title.innerText = space.name;
      spaceEl.appendChild(title);

      if (space.price > 0) {
        if (!space.ownerId) {
          const price = document.createElement('div');
          price.className = 'space-price';
          price.innerText = `kr. ${space.price.toLocaleString('da-DK')}`;
          spaceEl.appendChild(price);
        } else {
          const ownerIdx = currentGameState.players.findIndex(p => p.id === space.ownerId);
          const ownerPlayer = ownerIdx !== -1 ? currentGameState.players[ownerIdx] : null;
          const ownerColor = ownerIdx !== -1 ? PLAYER_COLORS[ownerIdx % PLAYER_COLORS.length] : '#888';
          const defaultIcons = ['🎩', '🚗', '🐕', '⛵', '🚲', '🍺'];
          const ownerIcon = ownerPlayer && ownerPlayer.id === mySession.playerId ? (mySession.tokenIcon || defaultIcons[ownerIdx % defaultIcons.length]) : (ownerPlayer ? defaultIcons[ownerIdx % defaultIcons.length] : '👤');

          // Cirkel i øverste hjørne med ejerens farve og ikon
          const ownerCircle = document.createElement('div');
          ownerCircle.className = 'owner-circle';
          ownerCircle.style.backgroundColor = ownerColor;
          ownerCircle.title = `Ejes af: ${space.ownerName || (ownerPlayer ? ownerPlayer.name : 'Ukendt')}`;
          ownerCircle.innerText = ownerIcon;
          spaceEl.appendChild(ownerCircle);

          // Vis leje på grunden
          const rentEl = document.createElement('div');
          rentEl.className = 'space-price';
          rentEl.style.color = '#555';
          rentEl.innerText = `Leje: ${space.currentRent.toLocaleString('da-DK')}`;
          spaceEl.appendChild(rentEl);

          // Farv hele baggrunden af feltet med en blød nuance af spillerens farve
          spaceEl.style.backgroundColor = `${ownerColor}22`; // Blød gennemsigtig spillertone
          spaceEl.style.borderColor = ownerColor;
          spaceEl.classList.add('is-owned');

          if (currentGameState.pendingRent && currentGameState.pendingRent.propertyIndex === space.index && currentGameState.pendingRent.iAmCreditor) {
            spaceEl.classList.add('has-pending-rent');
          }
        }
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

  // Håndtering af Lejeopkrævning (når en modspiller lander på din grund)
  const btnClaimHeader = document.getElementById('btn-claim-rent');

  if (currentGameState.pendingRent && currentGameState.pendingRent.iAmCreditor) {
    const rentInfo = currentGameState.pendingRent;
    if (btnClaimHeader) {
      btnClaimHeader.classList.remove('hidden');
      btnClaimHeader.innerText = `💰 Opkræv kr. ${rentInfo.amount.toLocaleString('da-DK')}`;
      btnClaimHeader.title = `${rentInfo.debtorName} er landet på ${rentInfo.propertyName}. Klik for at opkræve nu!`;
    }
  } else {
    if (btnClaimHeader) btnClaimHeader.classList.add('hidden');
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
      const currentSpace = currentGameState.spaces[curP.position];
      if (currentSpace && curP.balance < currentSpace.price) {
        // Har ikke råd
        buyActions.classList.add('hidden');
        btnEndTurn.classList.remove('hidden');
      } else {
        buyActions.classList.remove('hidden');
      }
    } else if (phase === 'ActionResolved') {
      btnEndTurn.classList.remove('hidden');
    }
  } else {
    turnWaitMsg.classList.remove('hidden');
    turnWaitMsg.innerText = `Afventer ${curP.name}...`;
  }

  // Seneste hændelse
  if (currentGameState.logs.length > 0) {
    const lastEventBox = document.getElementById('last-event-box');
    const latestLog = currentGameState.logs[currentGameState.logs.length - 1];
    lastEventBox.innerText = latestLog;
    lastEventBox.title = latestLog;
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

    const offeredItems = (t.offeredProperties || []).map(p => {
      const badge = getPropertyColorBadge(p.group || (currentGameState.spaces[p.index] ? currentGameState.spaces[p.index].group : null));
      return `${badge}<strong>${p.name}</strong>`;
    });
    if (t.offeredJailCards > 0) offeredItems.push(`🎟️ ${t.offeredJailCards}x Frikort`);
    const offeredStr = offeredItems.length > 0 ? offeredItems.join(', ') : 'Ingen grunde';

    const reqItems = (t.requestedProperties || []).map(p => {
      const badge = getPropertyColorBadge(p.group || (currentGameState.spaces[p.index] ? currentGameState.spaces[p.index].group : null));
      return `${badge}<strong>${p.name}</strong>`;
    });
    if (t.requestedJailCards > 0) reqItems.push(`🎟️ ${t.requestedJailCards}x Frikort`);
    const reqStr = reqItems.length > 0 ? reqItems.join(', ') : (t.propertyName || 'Ingen grunde');

    let cashText = '';
    if (t.cashAmount > 0) {
      cashText = ` + betaler dig <strong>kr. ${t.cashAmount.toLocaleString('da-DK')}</strong>`;
    } else if (t.cashAmount < 0) {
      cashText = ` (kræver <strong>kr. ${(-t.cashAmount).toLocaleString('da-DK')}</strong> af dig oveni)`;
    }

    div.innerHTML = `
      <div style="font-size: 0.8rem; line-height: 1.45;">
        <strong>${t.fromPlayer}</strong> tilbyder: 
        <div style="color: #3fb950; margin: 4px 0; display:flex; flex-wrap:wrap; align-items:center; gap:4px;">🎁 [${offeredStr}]${cashText}</div>
        til gengæld for dine:
        <div style="color: var(--accent-gold); margin: 4px 0; display:flex; flex-wrap:wrap; align-items:center; gap:4px;">🏠 [${reqStr}]</div>
      </div>
      <div class="trade-actions" style="margin-top: 6px;">
        <button class="btn-success" style="padding: 4px 10px; font-size: 0.75rem;" onclick="respondTrade(${t.id}, true)">Accepter Bytte</button>
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

    // Opkræv leje direkte på grunden hvis en modspiller er landet her
    const hasPendingRentHere = currentGameState.pendingRent && 
                               currentGameState.pendingRent.propertyIndex === space.index && 
                               currentGameState.pendingRent.iAmCreditor;

    if (hasPendingRentHere) {
      const rent = currentGameState.pendingRent;
      html += `
        <div style="background: rgba(46, 160, 67, 0.15); border: 1px solid #2ea043; border-radius: 6px; padding: 8px; margin-top: 8px;">
          <div style="font-size: 0.8rem; color: #3fb950; font-weight: 600;">
            ${rent.debtorName} er landet på grunden!
          </div>
          <button id="btn-claim-rent-detail" class="btn-claim-property">
            💰 Opkræv kr. ${rent.amount.toLocaleString('da-DK')} i leje
          </button>
        </div>
      `;
    }

    // Byd på grunden hvis den ejes af en modspiller
    if (space.ownerId && space.ownerId !== currentGameState.myPlayerId) {
      html += `<button id="btn-quick-bid" class="btn-primary" style="margin-top: 8px; width: 100%;">Byd på denne grund...</button>`;
    }
  }

  detailsEl.innerHTML = html;

  const btnClaimDetail = document.getElementById('btn-claim-rent-detail');
  if (btnClaimDetail) {
    btnClaimDetail.addEventListener('click', async () => {
      await triggerClaimRent();
    });
  }

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
  // Tjek om det er f.eks. "Ryk 3 felter tilbage"
  let forwardSteps = (targetPos - startPos + 40) % 40;
  let isBackward = ((startPos - targetPos + 40) % 40) === 3;

  const positions = {};
  if (currentGameState && currentGameState.players) {
    currentGameState.players.forEach(p => { positions[p.id] = p.position; });
  }

  let cur = startPos;

  if (isBackward) {
    // Ryk 3 felter baglæns
    for (let s = 1; s <= 3; s++) {
      cur = (cur - 1 + 40) % 40;
      positions[playerId] = cur;
      renderBoard(positions);
      await new Promise(r => setTimeout(r, 150));
    }
  } else {
    // Ryk fremad
    const delay = forwardSteps > 15 ? 80 : (forwardSteps > 8 ? 110 : 140);
    for (let s = 1; s <= forwardSteps; s++) {
      cur = (cur + 1) % 40;
      positions[playerId] = cur;
      renderBoard(positions);

      if (cur === 0) {
        const startEl = document.getElementById('space-0');
        if (startEl) {
          startEl.classList.add('highlight-pass');
          setTimeout(() => startEl.classList.remove('highlight-pass'), 600);
        }
      }

      await new Promise(r => setTimeout(r, delay));
    }
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
  nextState.players.forEach(p => { knownPlayerPositions[p.id] = p.position; });
  isAnimating = false;
  renderBoard();
  renderUI();
});

async function triggerClaimRent() {
  await fetch(`/api/rooms/${mySession.roomCode}/claimrent`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: mySession.token })
  });
  fetchGameState();
}

document.getElementById('btn-claim-rent').addEventListener('click', triggerClaimRent);

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
const myPropsContainer = document.getElementById('trade-my-props');
const targetPropsContainer = document.getElementById('trade-target-props');
const giveJailCardCheckbox = document.getElementById('trade-give-jailcard');
const wantJailCardCheckbox = document.getElementById('trade-want-jailcard');
const tradeCashType = document.getElementById('trade-cash-type');
const tradeCashAmount = document.getElementById('trade-cash-amount');

document.getElementById('btn-open-trade').addEventListener('click', () => {
  if (!currentGameState) return;
  openTradeModal();
});

document.getElementById('btn-cancel-trade').addEventListener('click', () => {
  tradeModal.classList.add('hidden');
});

function openTradeModal(preselectedTargetPropIndex = null, preselectedSellerId = null) {
  const myP = currentGameState.players.find(p => p.id === currentGameState.myPlayerId);
  if (!myP) return;

  // 1. Udfyld modspillere
  sellerSelect.innerHTML = '';
  const otherPlayers = currentGameState.players.filter(p => p.id !== myP.id && !p.isBankrupt);
  
  if (otherPlayers.length === 0) {
    alert('Der er ingen andre aktive modspillere at handle med.');
    return;
  }

  otherPlayers.forEach(other => {
    const opt = document.createElement('option');
    opt.value = other.id;
    opt.innerText = `${other.name} (${other.ownedCount} ejendomme, kr. ${other.balance.toLocaleString('da-DK')})`;
    sellerSelect.appendChild(opt);
  });

  if (preselectedSellerId) {
    sellerSelect.value = preselectedSellerId;
  }

  // 2. Vis mine grunde (Gives)
  myPropsContainer.innerHTML = '';
  if (myP.ownedProperties && myP.ownedProperties.length > 0) {
    myP.ownedProperties.forEach(prop => {
      const colorBadge = getPropertyColorBadge(prop.group || (currentGameState.spaces[prop.index] ? currentGameState.spaces[prop.index].group : null));
      const label = document.createElement('label');
      label.style.display = 'flex';
      label.style.alignItems = 'center';
      label.style.gap = '6px';
      label.style.cursor = 'pointer';
      label.style.padding = '3px 4px';
      label.style.borderRadius = '4px';
      label.style.background = '#141820';
      label.innerHTML = `
        <input type="checkbox" class="trade-my-prop-checkbox" value="${prop.index}" />
        <div style="display:flex; align-items:center; flex-wrap:wrap; gap:4px;">
          ${colorBadge}
          <span style="font-weight:600;">${prop.name}</span>
          <span style="color:#8b949e; font-size:0.72rem;">(kr. ${prop.price.toLocaleString('da-DK')})</span>
        </div>
      `;
      myPropsContainer.appendChild(label);
    });
  } else {
    myPropsContainer.innerHTML = '<span class="placeholder-text" style="font-size: 0.75rem;">Du ejer ingen grunde.</span>';
  }

  // Frikort hos mig
  if (giveJailCardCheckbox) {
    giveJailCardCheckbox.checked = false;
    giveJailCardCheckbox.disabled = (myP.getOutOfJailCards || 0) <= 0;
  }

  // Nulstil kontanter
  tradeCashAmount.value = 0;
  tradeCashType.value = 'give';

  // 3. Opdater modspillerens grunde
  updateTargetPlayerTradeItems(preselectedTargetPropIndex);
  sellerSelect.onchange = () => updateTargetPlayerTradeItems();

  tradeModal.classList.remove('hidden');
}

function openTradeModalWithProperty(propIndex, sellerId) {
  openTradeModal(propIndex, sellerId);
}

function updateTargetPlayerTradeItems(preselectedPropIndex = null) {
  const targetId = sellerSelect.value;
  const targetPlayer = currentGameState.players.find(p => p.id === targetId);
  targetPropsContainer.innerHTML = '';

  if (targetPlayer && targetPlayer.ownedProperties && targetPlayer.ownedProperties.length > 0) {
    targetPlayer.ownedProperties.forEach(prop => {
      const colorBadge = getPropertyColorBadge(prop.group || (currentGameState.spaces[prop.index] ? currentGameState.spaces[prop.index].group : null));
      const label = document.createElement('label');
      label.style.display = 'flex';
      label.style.alignItems = 'center';
      label.style.gap = '6px';
      label.style.cursor = 'pointer';
      label.style.padding = '3px 4px';
      label.style.borderRadius = '4px';
      label.style.background = '#141820';
      const isChecked = preselectedPropIndex === prop.index ? 'checked' : '';
      label.innerHTML = `
        <input type="checkbox" class="trade-target-prop-checkbox" value="${prop.index}" ${isChecked} />
        <div style="display:flex; align-items:center; flex-wrap:wrap; gap:4px;">
          ${colorBadge}
          <span style="font-weight:600;">${prop.name}</span>
          <span style="color:#8b949e; font-size:0.72rem;">(kr. ${prop.price.toLocaleString('da-DK')})</span>
        </div>
      `;
      targetPropsContainer.appendChild(label);
    });
  } else {
    targetPropsContainer.innerHTML = '<span class="placeholder-text" style="font-size: 0.75rem;">Modspilleren ejer ingen grunde.</span>';
  }

  if (wantJailCardCheckbox) {
    wantJailCardCheckbox.checked = false;
    wantJailCardCheckbox.disabled = !targetPlayer || (targetPlayer.getOutOfJailCards || 0) <= 0;
  }
}

document.getElementById('btn-send-trade').addEventListener('click', async () => {
  const toPlayerId = sellerSelect.value;
  if (!toPlayerId) return;

  // Saml tilbudte grunde fra mig
  const offeredIndices = [];
  document.querySelectorAll('.trade-my-prop-checkbox:checked').forEach(cb => {
    offeredIndices.push(parseInt(cb.value));
  });

  // Saml ønskede grunde fra modspiller
  const requestedIndices = [];
  document.querySelectorAll('.trade-target-prop-checkbox:checked').forEach(cb => {
    requestedIndices.push(parseInt(cb.value));
  });

  const offeredJailCards = giveJailCardCheckbox && giveJailCardCheckbox.checked ? 1 : 0;
  const requestedJailCards = wantJailCardCheckbox && wantJailCardCheckbox.checked ? 1 : 0;

  // Kontanter
  const rawCash = parseInt(tradeCashAmount.value) || 0;
  const cashAmount = tradeCashType.value === 'give' ? rawCash : -rawCash;

  if (offeredIndices.length === 0 && requestedIndices.length === 0 && offeredJailCards === 0 && requestedJailCards === 0 && rawCash === 0) {
    alert('Vælg venligst mindst én grund, et frikort eller et kontantbeløb at bytte med.');
    return;
  }

  const myP = currentGameState.players.find(p => p.id === currentGameState.myPlayerId);
  if (cashAmount > 0 && myP && myP.balance < cashAmount) {
    alert(`Du har kun kr. ${myP.balance.toLocaleString('da-DK')}, så du kan ikke tilbyde kr. ${cashAmount.toLocaleString('da-DK')} i kontanter.`);
    return;
  }

  await fetch(`/api/rooms/${mySession.roomCode}/trade/propose`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      token: mySession.token,
      toPlayerId: toPlayerId,
      offeredPropertyIndices: offeredIndices,
      requestedPropertyIndices: requestedIndices,
      cashAmount: cashAmount,
      offeredJailCards: offeredJailCards,
      requestedJailCards: requestedJailCards
    })
  });

  tradeModal.classList.add('hidden');
  showToast('Dit byttetilbud er sendt afsted!', 'info', '📨');
  fetchGameState();
});

// Hent og vis deployet version fra /version endpoint
async function loadAppVersion() {
  try {
    const res = await fetch('/version');
    if (res.ok) {
      const data = await res.json();
      const versionLabel = data.version.startsWith('v') ? data.version : `v${data.version}`;
      const shortCommit = data.commit && data.commit.length >= 7 ? ` (${data.commit.substring(0, 7)})` : '';
      const text = `${versionLabel}${shortCommit}`;
      
      const badge1 = document.getElementById('lobby-version-badge');
      const badge2 = document.getElementById('app-version-badge');
      if (badge1) {
        badge1.innerText = text;
        badge1.title = `Bygget: ${data.buildTime || 'N/A'} (${data.environment || 'Production'})`;
      }
      if (badge2) {
        badge2.innerText = text;
        badge2.title = `Bygget: ${data.buildTime || 'N/A'} (${data.environment || 'Production'})`;
      }
    }
  } catch {
    // Stille fallback hvis offline
  }
}

loadAppVersion();

// Hvis vi allerede har en session gemt, start automatisk opkobling
if (mySession.roomCode && mySession.token) {
  startPolling();
}
