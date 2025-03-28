self.onmessage = function (event) {
    debugger
    const result = event.data;
    const type = result.type;
    const data = result.data;
    const postMessage = result.postMessage;
    if (type === "rtsedsa") {
        event.source.postMessage({
            type: 'data-fetched',
            data: data
        });
    }

    self.addEventListener('message', function (event) {
        debugger
        if (event.data.type === 'fetch-data') {
            fetch('/data').then(function (response) {
                return response.json();
            }).then(function (data) {
                // Send a message back to the main thread with the data
                event.source.postMessage({
                    type: 'data-fetched',
                    data: data
                });
            });
        }
    });
};





