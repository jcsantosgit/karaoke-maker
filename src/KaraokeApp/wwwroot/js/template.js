// Simple Router
function navigateTo(page) {
    if (page === 'logout') {
        localStorage.removeItem('loggedInUser');
        window.location.href = '/';
        return;
    }
    window.location.href = page;
}

// Register Handler
function handleRegister(e) {
    e.preventDefault();
    const name = e.target.querySelector('input[type="text"]').value;
    const email = e.target.querySelector('input[type="email"]').value;
    const password = e.target.querySelector('input[type="password"]').value;
    const btn = e.target.querySelector('button');
    const originalText = btn.innerHTML;

    const users = JSON.parse(localStorage.getItem('users')) || [];
    const userExists = users.find(user => user.email === email);

    if (userExists) {
        alert('Este e-mail já está cadastrado!');
        return;
    }

    users.push({ name, email, password });
    localStorage.setItem('users', JSON.stringify(users));

    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Cadastrando...';

    setTimeout(() => {
        alert('Cadastro realizado com sucesso! Faça o login.');
        window.location.href = '/Login';
    }, 1000);
}

// Login Handler
function handleLogin(e) {
    e.preventDefault();
    const email = e.target.querySelector('input[type="email"]').value;
    const password = e.target.querySelector('input[type="password"]').value;
    const btn = e.target.querySelector('button');
    const originalText = btn.innerHTML;

    const users = JSON.parse(localStorage.getItem('users')) || [];
    const user = users.find(user => user.email === email && user.password === password);

    if (user) {
        btn.disabled = true;
        btn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Entrando...';

        setTimeout(() => {
            localStorage.setItem('loggedInUser', user.email);
            window.location.href = '/Audio';
        }, 1000);
    } else {
        alert('Usuário ou senha inválidos!');
    }
}

// Check if user is logged in
function checkLogin() {
    const user = localStorage.getItem('loggedInUser');
    if (!user) {
        window.location.href = '/Login';
    }
}

// Upload Simulation Logic
function startSimulation(filename) {
    const uploadZone = document.getElementById('upload-zone');
    const processingState = document.getElementById('processing-state');
    const successState = document.getElementById('success-state');
    const progressBar = document.getElementById('progress-bar');
    const percentageText = document.getElementById('percentage-text');
    const statusText = document.getElementById('status-text');
    const step1 = document.getElementById('step-1');
    const step2 = document.getElementById('step-2');
    const step3 = document.getElementById('step-3');

    function setStepActive(el) {
        el.classList.remove('text-gray-500');
        el.classList.add('text-white', 'font-bold');
        el.querySelector('i').className = 'fa-solid fa-circle-notch fa-spin text-brand-purple w-4';
    }
    function setStepDone(el) {
        el.querySelector('i').className = 'fa-solid fa-check text-green-500 w-4';
    }

    // Reset UI
    uploadZone.classList.add('hidden');
    processingState.classList.remove('hidden');
    progressBar.style.width = '0%';

    let progress = 0;
    const interval = setInterval(() => {
        progress += Math.random() * 5;
        if (progress > 100) progress = 100;

        progressBar.style.width = `${progress}%`;
        percentageText.innerText = `${Math.round(progress)}%`;

        if (progress < 30) {
            statusText.innerText = "Isolando instrumental...";
            setStepActive(step1);
        } else if (progress < 70) {
            setStepDone(step1);
            setStepActive(step2);
            statusText.innerText = "Sincronizando legendas...";
        } else if (progress < 98) {
            setStepDone(step2);
            setStepActive(step3);
            statusText.innerText = "Renderizando vídeo...";
        }

        if (progress === 100) {
            clearInterval(interval);
            setStepDone(step3);
            setTimeout(() => {
                processingState.classList.add('hidden');
                successState.classList.remove('hidden');
            }, 600);
        }
    }, 100);
}

function resetUpload() {
    document.getElementById('success-state').classList.add('hidden');
    document.getElementById('upload-zone').classList.remove('hidden');
    document.getElementById('file-upload').value = '';
    // Reset steps style
    [document.getElementById('step-1'), document.getElementById('step-2'), document.getElementById('step-3')].forEach(el => {
        el.className = 'flex items-center gap-3 text-sm text-gray-500';
        el.querySelector('i').className = 'fa-regular fa-circle w-4';
    });
}