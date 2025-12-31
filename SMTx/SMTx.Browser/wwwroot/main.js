console.log('main.js: Script loaded');

// Update test message
const testMsg = document.getElementById('test-message');
if (testMsg) {
    testMsg.textContent = 'main.js loaded, importing dotnet...';
}

try {
    console.log('main.js: Importing dotnet...');
    const { dotnet } = await import('./_framework/dotnet.js');
    console.log('main.js: dotnet imported successfully');

    if (testMsg) {
        testMsg.textContent = 'dotnet imported, creating runtime...';
    }

    const is_browser = typeof window != "undefined";
    if (!is_browser) throw new Error(`Expected to be running in a browser`);

    console.log('main.js: Creating .NET runtime...');
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    console.log('main.js: Getting config...');
    const config = dotnetRuntime.getConfig();
    console.log('main.js: Config:', config);

    if (testMsg) {
        testMsg.textContent = 'Runtime created, running main...';
    }

    console.log('main.js: Running main assembly:', config.mainAssemblyName);
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
    console.log('main.js: Main assembly execution completed');

    if (testMsg) {
        testMsg.textContent = 'Main assembly completed!';
        setTimeout(() => {
            if (testMsg) testMsg.style.display = 'none';
        }, 2000);
    }
} catch (error) {
    console.error('main.js: ERROR:', error);
    if (testMsg) {
        testMsg.textContent = 'ERROR: ' + error.message;
        testMsg.style.background = 'darkred';
    }
    throw error;
}
