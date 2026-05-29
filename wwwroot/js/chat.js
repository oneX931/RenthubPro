window.rentHubChat = (function () {
    let connection = null;
    let dotNetRef = null;

    async function start(ref) {
        dotNetRef = ref;

        if (connection) {
            try { await connection.stop(); } catch (e) { }
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/chathub')
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveMessage', function (msg) {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnMessageReceived', msg);
            }
        });

        try {
            await connection.start();
            return true;
        } catch (err) {
            console.error('Ошибка подключения к чату:', err);
            return false;
        }
    }

    async function send(recipientId, text) {
        if (!connection) return false;
        try {
            await connection.invoke('SendMessage', recipientId, text);
            return true;
        } catch (err) {
            console.error('Ошибка отправки:', err);
            return false;
        }
    }

    async function stop() {
        if (connection) {
            try { await connection.stop(); } catch (e) { }
            connection = null;
        }
        dotNetRef = null;
    }

    return { start: start, send: send, stop: stop };
})();

window.rentHubScrollBottom = function (elementId) {
    var el = document.getElementById(elementId);
    if (el) el.scrollTop = el.scrollHeight;
};
