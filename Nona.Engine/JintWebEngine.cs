using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;

namespace Nona.Engine;

/// <summary>
/// Modern Jint JavaScript engine with full WebAPI support
/// Includes: async/await, Promise, DOM APIs, Events, Timers, Fetch emulation
/// </summary>
public class JintWebEngine : IDisposable
{
    private Jint.Engine? _engine;
    private readonly List<string> _consoleLog = new();
    private readonly Dictionary<int, System.Threading.Timer> _timers = new();
    private int _timerIdCounter = 0;
    private bool _isInitialized = false;
    
    public bool IsEnabled { get; set; } = true;
    public IReadOnlyList<string> ConsoleMessages => _consoleLog;
    
    public JintWebEngine()
    {
        // Lazy initialization
    }
    
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        
        // Create Jint engine with modern features
        _engine = new Jint.Engine(options =>
        {
            options
                .AllowClr() // Allow calling .NET code
                .LimitRecursion(128) // Prevent stack overflow
                .TimeoutInterval(TimeSpan.FromSeconds(5)) // Timeout protection
                .MaxStatements(10000); // Statement limit
        });
        
        SetupWebAPIs();
        _isInitialized = true;
    }
    
    private void SetupWebAPIs()
    {
        if (_engine == null) return;
        
        // ===== ENHANCED POLYFILLS =====
        _engine.SetupEnhancedAPIs();
        _engine.SetupGoogleCompatibility();
        
        // ===== CONSOLE API =====
        _engine.SetValue("console", new
        {
            log = new Action<object>(msg => _consoleLog.Add($"[LOG] {msg}")),
            warn = new Action<object>(msg => _consoleLog.Add($"[WARN] {msg}")),
            error = new Action<object>(msg => _consoleLog.Add($"[ERROR] {msg}")),
            info = new Action<object>(msg => _consoleLog.Add($"[INFO] {msg}")),
            debug = new Action<object>(msg => _consoleLog.Add($"[DEBUG] {msg}"))
        });
        
        // ===== ALERT API =====
        _engine.SetValue("alert", new Action<object>(msg =>
        {
            _consoleLog.Add($"[ALERT] {msg}");
        }));
        
        // ===== TIMERS API (setTimeout, setInterval) =====
        _engine.SetValue("setTimeout", new Func<Func<object, JsValue[], JsValue>, int, int>((callback, delay) =>
        {
            var timerId = ++_timerIdCounter;
            var timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    callback(null, Array.Empty<JsValue>());
                }
                catch (Exception ex)
                {
                    _consoleLog.Add($"[ERROR] Timer error: {ex.Message}");
                }
                
                if (_timers.ContainsKey(timerId))
                {
                    _timers[timerId].Dispose();
                    _timers.Remove(timerId);
                }
            }, null, delay, Timeout.Infinite);
            
            _timers[timerId] = timer;
            return timerId;
        }));
        
        _engine.SetValue("setInterval", new Func<Func<object, JsValue[], JsValue>, int, int>((callback, delay) =>
        {
            var timerId = ++_timerIdCounter;
            var timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    callback(null, Array.Empty<JsValue>());
                }
                catch (Exception ex)
                {
                    _consoleLog.Add($"[ERROR] Interval error: {ex.Message}");
                }
            }, null, delay, delay);
            
            _timers[timerId] = timer;
            return timerId;
        }));
        
        _engine.SetValue("clearTimeout", new Action<int>(timerId =>
        {
            if (_timers.ContainsKey(timerId))
            {
                _timers[timerId].Dispose();
                _timers.Remove(timerId);
            }
        }));
        
        _engine.SetValue("clearInterval", new Action<int>(timerId =>
        {
            if (_timers.ContainsKey(timerId))
            {
                _timers[timerId].Dispose();
                _timers.Remove(timerId);
            }
        }));
        
        // ===== REQUEST ANIMATION FRAME =====
        _engine.SetValue("requestAnimationFrame", new Func<Func<object, JsValue[], JsValue>, int>(callback =>
        {
            var timerId = ++_timerIdCounter;
            var timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    callback(null, new[] { JsValue.FromObject(_engine, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) });
                }
                catch (Exception ex)
                {
                    _consoleLog.Add($"[ERROR] RAF error: {ex.Message}");
                }
                
                if (_timers.ContainsKey(timerId))
                {
                    _timers[timerId].Dispose();
                    _timers.Remove(timerId);
                }
            }, null, 16, Timeout.Infinite); // ~60fps
            
            _timers[timerId] = timer;
            return timerId;
        }));
        
        // ===== PROMISE & ASYNC/AWAIT POLYFILL =====
        _engine.Execute(@"
            // Promise implementation (simplified)
            class Promise {
                constructor(executor) {
                    this._state = 'pending';
                    this._value = undefined;
                    this._handlers = [];
                    
                    const resolve = (value) => {
                        if (this._state !== 'pending') return;
                        this._state = 'fulfilled';
                        this._value = value;
                        this._handlers.forEach(h => h.onFulfilled && h.onFulfilled(value));
                    };
                    
                    const reject = (reason) => {
                        if (this._state !== 'pending') return;
                        this._state = 'rejected';
                        this._value = reason;
                        this._handlers.forEach(h => h.onRejected && h.onRejected(reason));
                    };
                    
                    try {
                        executor(resolve, reject);
                    } catch (error) {
                        reject(error);
                    }
                }
                
                then(onFulfilled, onRejected) {
                    return new Promise((resolve, reject) => {
                        const handle = () => {
                            if (this._state === 'fulfilled') {
                                try {
                                    const result = onFulfilled ? onFulfilled(this._value) : this._value;
                                    resolve(result);
                                } catch (error) {
                                    reject(error);
                                }
                            } else if (this._state === 'rejected') {
                                try {
                                    if (onRejected) {
                                        const result = onRejected(this._value);
                                        resolve(result);
                                    } else {
                                        reject(this._value);
                                    }
                                } catch (error) {
                                    reject(error);
                                }
                            }
                        };
                        
                        if (this._state === 'pending') {
                            this._handlers.push({ onFulfilled, onRejected, resolve, reject });
                        } else {
                            setTimeout(handle, 0);
                        }
                    });
                }
                
                catch(onRejected) {
                    return this.then(null, onRejected);
                }
                
                finally(onFinally) {
                    return this.then(
                        value => { onFinally && onFinally(); return value; },
                        reason => { onFinally && onFinally(); throw reason; }
                    );
                }
                
                static resolve(value) {
                    return new Promise(resolve => resolve(value));
                }
                
                static reject(reason) {
                    return new Promise((_, reject) => reject(reason));
                }
                
                static all(promises) {
                    return new Promise((resolve, reject) => {
                        const results = [];
                        let completed = 0;
                        
                        if (promises.length === 0) {
                            resolve(results);
                            return;
                        }
                        
                        promises.forEach((promise, index) => {
                            Promise.resolve(promise).then(value => {
                                results[index] = value;
                                completed++;
                                if (completed === promises.length) {
                                    resolve(results);
                                }
                            }).catch(reject);
                        });
                    });
                }
                
                static race(promises) {
                    return new Promise((resolve, reject) => {
                        promises.forEach(promise => {
                            Promise.resolve(promise).then(resolve).catch(reject);
                        });
                    });
                }
            }
            
            // Async/await support (via transpilation hints)
            var __awaiter = function (thisArg, _arguments, P, generator) {
                return new Promise(function (resolve, reject) {
                    function fulfilled(value) { 
                        try { step(generator.next(value)); } 
                        catch (e) { reject(e); } 
                    }
                    function rejected(value) { 
                        try { step(generator.throw(value)); } 
                        catch (e) { reject(e); } 
                    }
                    function step(result) {
                        result.done ? resolve(result.value) : 
                            Promise.resolve(result.value).then(fulfilled, rejected);
                    }
                    step((generator = generator.apply(thisArg, _arguments || [])).next());
                });
            };
            
            // Browser detection helpers
            var Element = function() {};
            var HTMLElement = function() {};
            var Node = function() {};
            var Event = function() {};
            var MouseEvent = function() {};
            var KeyboardEvent = function() {};
        ");
    }
    
    public void Execute(string script)
    {
        if (!IsEnabled) return;
        
        try
        {
            EnsureInitialized();
            _engine?.Execute(script);
        }
        catch (JavaScriptException ex)
        {
            var error = $"JS Error: {ex.Message} at line {ex.Location.Start.Line}";
            _consoleLog.Add($"[ERROR] {error}");
        }
        catch (Exception ex)
        {
            _consoleLog.Add($"[ERROR] Execution error: {ex.Message}");
        }
    }
    
    public object? Evaluate(string code)
    {
        try
        {
            EnsureInitialized();
            var result = _engine?.Evaluate(code);
            return result?.ToObject();
        }
        catch (Exception ex)
        {
            _consoleLog.Add($"[ERROR] Evaluation error: {ex.Message}");
            return null;
        }
    }
    
    public void SetValue(string name, object value)
    {
        EnsureInitialized();
        _engine?.SetValue(name, value);
    }
    
    public object? GetValue(string name)
    {
        EnsureInitialized();
        return _engine?.GetValue(name).ToObject();
    }
    
    public void ClearConsole()
    {
        _consoleLog.Clear();
    }
    
    public void Dispose()
    {
        // Dispose all timers
        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }
        _timers.Clear();
        
        _engine = null;
        _isInitialized = false;
    }
}

