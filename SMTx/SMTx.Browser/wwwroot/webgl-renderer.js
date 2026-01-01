// WebGL Renderer for 3D Map Visualization
// Provides GPU-accelerated rendering of solar systems and stargate links
// This is an ES module for JSImport compatibility

let gl = null;
let canvas = null;
let canvasId = null;
const canvasElements = new Map(); // Track all canvas elements

// Shader programs
let systemShaderProgram = null;
let linkShaderProgram = null;

// Buffers
let systemBuffer = null;
let linkBuffer = null;

// Uniform locations
let systemUniforms = {};
let linkUniforms = {};

// Current data
let systemCount = 0;
let linkCount = 0;
let linkTypes = null; // Array of link type indices (0=regular, 1=constellation, 2=regional)

// Check WebGL support
export function isWebGLSupported() {
    try {
        const testCanvas = document.createElement('canvas');
        return !!(testCanvas.getContext('webgl') || testCanvas.getContext('experimental-webgl'));
    } catch (e) {
        return false;
    }
}

// Expose WebGL support check globally as well
window.isWebGLSupported = isWebGLSupported;

// Log that the script is loaded
console.log('WebGL: webgl-renderer.js loaded as ES module');

// Helper function to create canvas element
export function createWebGLCanvas(id, width, height) {
    try {
        // Find the Avalonia root element
        const avaloniaRoot = document.getElementById('out');
        if (!avaloniaRoot) {
            console.error('WebGL: Avalonia root element not found');
            return false;
        }
        
        // Get the actual size of the Avalonia container
        const rect = avaloniaRoot.getBoundingClientRect();
        const actualWidth = width || rect.width || window.innerWidth || 800;
        const actualHeight = height || rect.height || window.innerHeight || 600;
        
        console.log('WebGL: Creating canvas with size:', actualWidth, 'x', actualHeight);
        console.log('WebGL: Avalonia root rect:', rect);
        
        // Create canvas element
        const canvas = document.createElement('canvas');
        canvas.id = id;
        canvas.width = actualWidth;
        canvas.height = actualHeight;
        canvas.style.position = 'fixed'; // Use fixed to overlay on top
        canvas.style.top = '0';
        canvas.style.left = '0';
        canvas.style.width = actualWidth + 'px';
        canvas.style.height = actualHeight + 'px';
        canvas.style.pointerEvents = 'none'; // Allow mouse events to pass through to Avalonia
        canvas.style.zIndex = '9999'; // High z-index to be on top
        canvas.style.backgroundColor = 'rgba(0, 0, 0, 0)'; // Transparent black
        canvas.style.display = 'block'; // Ensure it's visible
        canvas.style.visibility = 'visible'; // Explicitly set visibility
        
        // Append to body to ensure it's on top of everything
        document.body.appendChild(canvas);
        canvasElements.set(id, canvas);
        
        console.log('WebGL: Canvas created and appended:', id, 'Size:', actualWidth, 'x', actualHeight);
        console.log('WebGL: Canvas element:', canvas);
        console.log('WebGL: Canvas computed style:', window.getComputedStyle(canvas).display);
        return true;
    } catch (e) {
        console.error('WebGL: Error creating canvas:', e);
        console.error('WebGL: Stack trace:', e.stack);
        return false;
    }
}

// Helper function to remove canvas element
export function removeWebGLCanvas(id) {
    try {
        const canvas = canvasElements.get(id);
        if (canvas && canvas.parentNode) {
            canvas.parentNode.removeChild(canvas);
            canvasElements.delete(id);
            console.log('WebGL: Canvas removed:', id);
            return true;
        }
        return false;
    } catch (e) {
        console.error('WebGL: Error removing canvas:', e);
        return false;
    }
}

// Initialize WebGL context
const webglRenderer = {
    init: function(canvasElementId) {
        console.log('WebGL: init called with canvas ID:', canvasElementId);
        canvasId = canvasElementId;
        canvas = canvasElements.get(canvasElementId) || document.getElementById(canvasElementId);
        
        if (!canvas) {
            console.error('WebGL: Canvas element not found:', canvasElementId);
            console.error('WebGL: Available canvas elements:', Array.from(canvasElements.keys()));
            return false;
        }
        
        console.log('WebGL: Canvas found, size:', canvas.width, 'x', canvas.height);
        
        if (!isWebGLSupported()) {
            console.error('WebGL: WebGL is not supported in this browser');
            return false;
        }
        
        // Get WebGL context
        gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
        
        if (!gl) {
            console.error('WebGL: Failed to get WebGL context');
            return false;
        }
        
        console.log('WebGL: Context initialized successfully');
        
        // Set viewport
        gl.viewport(0, 0, canvas.width, canvas.height);
        
        // Enable blending for transparency
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
        
        // Initialize shaders
        if (!initShaders()) {
            return false;
        }
        
        // Initialize buffers
        initBuffers();
        
        return true;
    },
    
    resize: function(width, height) {
        if (!canvas || !gl) {
            console.warn('WebGL: Cannot resize - canvas or gl not available');
            return;
        }
        
        if (width <= 0 || height <= 0) {
            console.warn('WebGL: Invalid resize dimensions:', width, height);
            return;
        }
        
        canvas.width = width;
        canvas.height = height;
        gl.viewport(0, 0, width, height);
        console.log('WebGL: Canvas resized to:', width, 'x', height);
    },
    
    clear: function() {
        if (!gl) return;
        
        // Clear with transparent black to see through to Avalonia content
        gl.clearColor(0.0, 0.0, 0.0, 0.0); // Transparent background
        gl.clear(gl.COLOR_BUFFER_BIT);
    },
    
    updateSystems: function(systemsArrayJson, count) {
        if (!gl || !systemBuffer) {
            console.warn('WebGL: Cannot update systems - gl or buffer not available');
            return;
        }
        
        systemCount = count;
        
        if (count === 0) {
            console.warn('WebGL: No systems to render');
            return;
        }
        
        // Parse JSON string to array
        let systemsArray;
        try {
            systemsArray = JSON.parse(systemsArrayJson);
        } catch (e) {
            console.error('WebGL: Failed to parse systems JSON:', e);
            return;
        }
        
        console.log('WebGL: Updating systems:', count, 'systems, array length:', systemsArray.length);
        
        // Update buffer with system positions and sizes
        gl.bindBuffer(gl.ARRAY_BUFFER, systemBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(systemsArray), gl.DYNAMIC_DRAW);
        
        // Log first few systems for debugging
        if (count > 0 && systemsArray.length >= 3) {
            console.log('WebGL: First system at:', systemsArray[0], systemsArray[1], 'size:', systemsArray[2]);
        }
    },
    
    updateLinks: function(linksArrayJson, linkTypesArrayJson, count) {
        if (!gl || !linkBuffer) return;
        
        linkCount = count;
        
        if (count === 0) {
            return;
        }
        
        // Parse JSON strings to arrays
        let linksArray, linkTypesArray;
        try {
            linksArray = JSON.parse(linksArrayJson);
            linkTypesArray = JSON.parse(linkTypesArrayJson);
        } catch (e) {
            console.error('WebGL: Failed to parse links JSON:', e);
            return;
        }
        
        // Store link types for color selection
        if (linkTypesArray) {
            linkTypes = new Uint8Array(linkTypesArray);
        }
        
        // Update buffer with link endpoints
        gl.bindBuffer(gl.ARRAY_BUFFER, linkBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(linksArray), gl.DYNAMIC_DRAW);
    },
    
    render: function() {
        if (!gl) {
            console.warn('WebGL: Cannot render - gl not available');
            return;
        }
        
        if (!canvas) {
            console.warn('WebGL: Cannot render - canvas not available');
            return;
        }
        
        console.log('WebGL: Rendering - systems:', systemCount, 'links:', linkCount);
        console.log('WebGL: Canvas size:', canvas.width, 'x', canvas.height);
        console.log('WebGL: Canvas position:', canvas.style.position, 'top:', canvas.style.top, 'left:', canvas.style.left);
        console.log('WebGL: Canvas z-index:', canvas.style.zIndex);
        console.log('WebGL: Canvas display:', window.getComputedStyle(canvas).display);
        console.log('WebGL: Canvas visibility:', window.getComputedStyle(canvas).visibility);
        console.log('WebGL: Viewport:', gl.canvas.width, 'x', gl.canvas.height);
        
        // Clear the canvas
        this.clear();
        
        // Render links first (so systems appear on top)
        if (linkCount > 0) {
            console.log('WebGL: Rendering links');
            renderLinks();
        }
        
        // Render systems
        if (systemCount > 0) {
            console.log('WebGL: Rendering systems');
            renderSystems();
        } else {
            console.warn('WebGL: No systems to render (systemCount is 0)');
        }
        
        // Check for errors after rendering
        let error = gl.getError();
        if (error !== gl.NO_ERROR) {
            console.error('WebGL: Error after render:', error, 'Error code:', error);
        } else {
            console.log('WebGL: Render completed successfully');
        }
        
        // Check for more errors
        while ((error = gl.getError()) !== gl.NO_ERROR) {
            console.error('WebGL: Additional error:', error);
        }
    },
    
    dispose: function() {
        if (gl) {
            if (systemBuffer) {
                gl.deleteBuffer(systemBuffer);
                systemBuffer = null;
            }
            if (linkBuffer) {
                gl.deleteBuffer(linkBuffer);
                linkBuffer = null;
            }
            if (systemShaderProgram) {
                gl.deleteProgram(systemShaderProgram);
                systemShaderProgram = null;
            }
            if (linkShaderProgram) {
                gl.deleteProgram(linkShaderProgram);
                linkShaderProgram = null;
            }
        }
        gl = null;
        canvas = null;
    }
};

// Initialize shaders
function initShaders() {
    // System shader (renders circles as points)
    const systemVertexShaderSource = `
        attribute vec2 a_position;
        attribute float a_size;
        uniform vec2 u_resolution;
        
        void main() {
            // Convert from pixels to clip space
            vec2 zeroToOne = a_position / u_resolution;
            vec2 zeroToTwo = zeroToOne * 2.0;
            vec2 clipSpace = zeroToTwo - 1.0;
            
            gl_Position = vec4(clipSpace * vec2(1, -1), 0, 1);
            gl_PointSize = a_size;
        }
    `;
    
    const systemFragmentShaderSource = `
        precision mediump float;
        uniform vec4 u_color;
        
        void main() {
            // Draw a circle
            vec2 coord = gl_PointCoord - vec2(0.5);
            float dist = length(coord);
            if (dist > 0.5) {
                discard;
            }
            gl_FragColor = u_color;
        }
    `;
    
    // Link shader (renders lines)
    const linkVertexShaderSource = `
        attribute vec2 a_position;
        uniform vec2 u_resolution;
        
        void main() {
            vec2 zeroToOne = a_position / u_resolution;
            vec2 zeroToTwo = zeroToOne * 2.0;
            vec2 clipSpace = zeroToTwo - 1.0;
            
            gl_Position = vec4(clipSpace * vec2(1, -1), 0, 1);
        }
    `;
    
    const linkFragmentShaderSource = `
        precision mediump float;
        uniform vec4 u_color;
        
        void main() {
            gl_FragColor = u_color;
        }
    `;
    
    systemShaderProgram = createShaderProgram(systemVertexShaderSource, systemFragmentShaderSource);
    if (!systemShaderProgram) return false;
    
    linkShaderProgram = createShaderProgram(linkVertexShaderSource, linkFragmentShaderSource);
    if (!linkShaderProgram) return false;
    
    // Get uniform locations for system shader
    systemUniforms.resolution = gl.getUniformLocation(systemShaderProgram, 'u_resolution');
    systemUniforms.color = gl.getUniformLocation(systemShaderProgram, 'u_color');
    
    // Get uniform locations for link shader
    linkUniforms.resolution = gl.getUniformLocation(linkShaderProgram, 'u_resolution');
    linkUniforms.color = gl.getUniformLocation(linkShaderProgram, 'u_color');
    
    return true;
}

// Create shader program from source
function createShaderProgram(vertexSource, fragmentSource) {
    const vertexShader = createShader(gl.VERTEX_SHADER, vertexSource);
    const fragmentShader = createShader(gl.FRAGMENT_SHADER, fragmentSource);
    
    if (!vertexShader || !fragmentShader) {
        return null;
    }
    
    const program = gl.createProgram();
    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);
    
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        console.error('WebGL: Shader program link error:', gl.getProgramInfoLog(program));
        gl.deleteProgram(program);
        return null;
    }
    
    return program;
}

// Create shader from source
function createShader(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        console.error('WebGL: Shader compile error:', gl.getShaderInfoLog(shader));
        gl.deleteShader(shader);
        return null;
    }
    
    return shader;
}

// Initialize buffers
function initBuffers() {
    // System buffer: [x, y, size] for each system
    systemBuffer = gl.createBuffer();
    
    // Link buffer: [x1, y1, x2, y2] for each link
    linkBuffer = gl.createBuffer();
}

// Render systems as points
function renderSystems() {
    if (systemCount === 0) {
        console.warn('WebGL: renderSystems called but systemCount is 0');
        return;
    }
    
    gl.useProgram(systemShaderProgram);
    
    // Set uniforms
    gl.uniform2f(systemUniforms.resolution, canvas.width, canvas.height);
    gl.uniform4f(systemUniforms.color, 1.0, 1.0, 1.0, 1.0); // White
    
    // Bind buffer
    gl.bindBuffer(gl.ARRAY_BUFFER, systemBuffer);
    
    // Set up position attribute (x, y)
    const positionLocation = gl.getAttribLocation(systemShaderProgram, 'a_position');
    if (positionLocation < 0) {
        console.error('WebGL: a_position attribute not found');
        return;
    }
    gl.enableVertexAttribArray(positionLocation);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 12, 0); // 12 bytes per system (x, y, size)
    
    // Set up size attribute
    const sizeLocation = gl.getAttribLocation(systemShaderProgram, 'a_size');
    if (sizeLocation < 0) {
        console.error('WebGL: a_size attribute not found');
        return;
    }
    gl.enableVertexAttribArray(sizeLocation);
    gl.vertexAttribPointer(sizeLocation, 1, gl.FLOAT, false, 12, 8); // Offset 8 bytes
    
    // Draw points
    console.log('WebGL: Drawing', systemCount, 'points');
    gl.drawArrays(gl.POINTS, 0, systemCount);
    
    // Check for errors
    const error = gl.getError();
    if (error !== gl.NO_ERROR) {
        console.error('WebGL: Error during drawArrays:', error);
    }
}

// Render links as lines
function renderLinks() {
    if (linkCount === 0) return;
    
    gl.useProgram(linkShaderProgram);
    
    // Set uniforms
    gl.uniform2f(linkUniforms.resolution, canvas.width, canvas.height);
    
    // Bind buffer
    gl.bindBuffer(gl.ARRAY_BUFFER, linkBuffer);
    
    // Set up position attribute
    const positionLocation = gl.getAttribLocation(linkShaderProgram, 'a_position');
    gl.enableVertexAttribArray(positionLocation);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);
    
    // Render links by type with different colors
    if (linkTypes && linkTypes.length === linkCount) {
        // Render regular links (gray)
        renderLinksByType(0, 0.5, 0.5, 0.5, 1.0);
        
        // Render constellation links (cyan)
        renderLinksByType(1, 0.0, 1.0, 1.0, 1.0);
        
        // Render regional links (red)
        renderLinksByType(2, 1.0, 0.0, 0.0, 1.0);
    } else {
        // Fallback: render all links as gray
        gl.uniform4f(linkUniforms.color, 0.5, 0.5, 0.5, 1.0);
        gl.drawArrays(gl.LINES, 0, linkCount * 2);
    }
}

// Helper to render links of a specific type
function renderLinksByType(type, r, g, b, a) {
    // Count links of this type and find their positions
    const indices = [];
    for (let i = 0; i < linkCount; i++) {
        if (linkTypes[i] === type) {
            indices.push(i);
        }
    }
    
    if (indices.length === 0) return;
    
    // Set color
    gl.uniform4f(linkUniforms.color, r, g, b, a);
    
    // Draw each link of this type (each link is 2 vertices)
    for (const idx of indices) {
        gl.drawArrays(gl.LINES, idx * 2, 2);
    }
}

// Expose webglRenderer to global scope
window.webglRenderer = webglRenderer;

// Export individual methods for JSImport
export const webglRendererInit = webglRenderer.init.bind(webglRenderer);
export const webglRendererResize = webglRenderer.resize.bind(webglRenderer);
export const webglRendererUpdateSystems = webglRenderer.updateSystems.bind(webglRenderer);
export const webglRendererUpdateLinks = webglRenderer.updateLinks.bind(webglRenderer);
export const webglRendererRender = webglRenderer.render.bind(webglRenderer);
export const webglRendererDispose = webglRenderer.dispose.bind(webglRenderer);

// Also expose to window for backward compatibility
window.webglRendererInit = webglRendererInit;
window.webglRendererResize = webglRendererResize;
window.webglRendererUpdateSystems = webglRendererUpdateSystems;
window.webglRendererUpdateLinks = webglRendererUpdateLinks;
window.webglRendererRender = webglRendererRender;
window.webglRendererDispose = webglRendererDispose;
