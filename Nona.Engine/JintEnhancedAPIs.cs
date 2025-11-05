using System;
using System.Collections.Generic;
using System.Linq;
using Jint;
using Jint.Native;

namespace Nona.Engine;

/// <summary>
/// Enhanced APIs for better Google compatibility
/// </summary>
public static class JintEnhancedAPIs
{
    /// <summary>
    /// Setup enhanced global APIs that Google needs
    /// </summary>
    public static void SetupEnhancedAPIs(this Jint.Engine engine)
    {
        // Add Array methods
        engine.Execute(@"
            if (!Array.prototype.forEach) {
                Array.prototype.forEach = function(callback, thisArg) {
                    for (var i = 0; i < this.length; i++) {
                        callback.call(thisArg, this[i], i, this);
                    }
                };
            }
            
            if (!Array.prototype.map) {
                Array.prototype.map = function(callback, thisArg) {
                    var result = [];
                    for (var i = 0; i < this.length; i++) {
                        result.push(callback.call(thisArg, this[i], i, this));
                    }
                    return result;
                };
            }
            
            if (!Array.prototype.filter) {
                Array.prototype.filter = function(callback, thisArg) {
                    var result = [];
                    for (var i = 0; i < this.length; i++) {
                        if (callback.call(thisArg, this[i], i, this)) {
                            result.push(this[i]);
                        }
                    }
                    return result;
                };
            }
            
            if (!Array.prototype.find) {
                Array.prototype.find = function(callback, thisArg) {
                    for (var i = 0; i < this.length; i++) {
                        if (callback.call(thisArg, this[i], i, this)) {
                            return this[i];
                        }
                    }
                    return undefined;
                };
            }
            
            if (!Array.prototype.findIndex) {
                Array.prototype.findIndex = function(callback, thisArg) {
                    for (var i = 0; i < this.length; i++) {
                        if (callback.call(thisArg, this[i], i, this)) {
                            return i;
                        }
                    }
                    return -1;
                };
            }
            
            if (!Array.prototype.includes) {
                Array.prototype.includes = function(searchElement, fromIndex) {
                    fromIndex = fromIndex || 0;
                    for (var i = fromIndex; i < this.length; i++) {
                        if (this[i] === searchElement) {
                            return true;
                        }
                    }
                    return false;
                };
            }
            
            if (!Array.isArray) {
                Array.isArray = function(obj) {
                    return Object.prototype.toString.call(obj) === '[object Array]';
                };
            }
            
            // Add Object methods
            if (!Object.keys) {
                Object.keys = function(obj) {
                    var keys = [];
                    for (var key in obj) {
                        if (obj.hasOwnProperty(key)) {
                            keys.push(key);
                        }
                    }
                    return keys;
                };
            }
            
            if (!Object.values) {
                Object.values = function(obj) {
                    var values = [];
                    for (var key in obj) {
                        if (obj.hasOwnProperty(key)) {
                            values.push(obj[key]);
                        }
                    }
                    return values;
                };
            }
            
            if (!Object.entries) {
                Object.entries = function(obj) {
                    var entries = [];
                    for (var key in obj) {
                        if (obj.hasOwnProperty(key)) {
                            entries.push([key, obj[key]]);
                        }
                    }
                    return entries;
                };
            }
            
            if (!Object.assign) {
                Object.assign = function(target) {
                    for (var i = 1; i < arguments.length; i++) {
                        var source = arguments[i];
                        for (var key in source) {
                            if (source.hasOwnProperty(key)) {
                                target[key] = source[key];
                            }
                        }
                    }
                    return target;
                };
            }
            
            // Add String methods
            if (!String.prototype.trim) {
                String.prototype.trim = function() {
                    return this.replace(/^\s+|\s+$/g, '');
                };
            }
            
            if (!String.prototype.startsWith) {
                String.prototype.startsWith = function(searchString, position) {
                    position = position || 0;
                    return this.indexOf(searchString, position) === position;
                };
            }
            
            if (!String.prototype.endsWith) {
                String.prototype.endsWith = function(searchString, length) {
                    if (length === undefined || length > this.length) {
                        length = this.length;
                    }
                    return this.substring(length - searchString.length, length) === searchString;
                };
            }
            
            if (!String.prototype.includes) {
                String.prototype.includes = function(searchString, position) {
                    position = position || 0;
                    return this.indexOf(searchString, position) !== -1;
                };
            }
            
            if (!String.prototype.repeat) {
                String.prototype.repeat = function(count) {
                    var result = '';
                    for (var i = 0; i < count; i++) {
                        result += this;
                    }
                    return result;
                };
            }
            
            // Add JSON (simplified)
            if (typeof JSON === 'undefined') {
                window.JSON = {
                    parse: function(text) {
                        return eval('(' + text + ')');
                    },
                    stringify: function(obj) {
                        if (obj === null) return 'null';
                        if (obj === undefined) return undefined;
                        if (typeof obj === 'string') return '""' + obj + '""';
                        if (typeof obj === 'number' || typeof obj === 'boolean') return String(obj);
                        return '{}';
                    }
                };
            }
            
            // Add Date.now
            if (!Date.now) {
                Date.now = function() {
                    return new Date().getTime();
                };
            }
            
            // Add Function.prototype.bind
            if (!Function.prototype.bind) {
                Function.prototype.bind = function(oThis) {
                    var aArgs = Array.prototype.slice.call(arguments, 1);
                    var fToBind = this;
                    var fNOP = function() {};
                    var fBound = function() {
                        return fToBind.apply(
                            this instanceof fNOP ? this : oThis,
                            aArgs.concat(Array.prototype.slice.call(arguments))
                        );
                    };
                    fNOP.prototype = this.prototype;
                    fBound.prototype = new fNOP();
                    return fBound;
                };
            }
        ");
    }
    
    /// <summary>
    /// Setup Google-specific compatibility
    /// </summary>
    public static void SetupGoogleCompatibility(this Jint.Engine engine)
    {
        engine.Execute(@"
            // Google detection workarounds
            if (typeof window !== 'undefined') {
                // Fake some Google-specific globals
                window._F_installCss = function() {};
                window._F_jsUrl = '';
                window.gbar = window.gbar || {};
                window.gbar.qs = window.gbar.qs || {};
                
                // Fake some common browser features
                window.matchMedia = function(query) {
                    return {
                        matches: false,
                        media: query,
                        addListener: function() {},
                        removeListener: function() {}
                    };
                };
                
                window.getComputedStyle = function(element, pseudoElt) {
                    return element.style || {
                        getPropertyValue: function(prop) { return ''; }
                    };
                };
                
                window.getSelection = function() {
                    return {
                        toString: function() { return ''; },
                        rangeCount: 0,
                        removeAllRanges: function() {},
                        addRange: function() {}
                    };
                };
                
                // Document methods
                if (document) {
                    document.createRange = function() {
                        return {
                            setStart: function() {},
                            setEnd: function() {},
                            commonAncestorContainer: null,
                            getBoundingClientRect: function() {
                                return { top: 0, left: 0, width: 0, height: 0, right: 0, bottom: 0 };
                            }
                        };
                    };
                    
                    document.createNodeIterator = function() {
                        return {
                            nextNode: function() { return null; }
                        };
                    };
                    
                    document.createTreeWalker = function() {
                        return {
                            nextNode: function() { return null; }
                        };
                    };
                }
                
                // Element prototype enhancements
                if (typeof Element !== 'undefined' && Element.prototype) {
                    if (!Element.prototype.matches) {
                        Element.prototype.matches = function(selector) {
                            return false; // Simplified
                        };
                    }
                    
                    if (!Element.prototype.closest) {
                        Element.prototype.closest = function(selector) {
                            return null; // Simplified
                        };
                    }
                    
                    if (!Element.prototype.getBoundingClientRect) {
                        Element.prototype.getBoundingClientRect = function() {
                            return {
                                top: 0,
                                left: 0,
                                right: this.offsetWidth || 0,
                                bottom: this.offsetHeight || 0,
                                width: this.offsetWidth || 0,
                                height: this.offsetHeight || 0,
                                x: 0,
                                y: 0
                            };
                        };
                    }
                }
                
                // Add MutationObserver stub
                window.MutationObserver = window.MutationObserver || function() {
                    return {
                        observe: function() {},
                        disconnect: function() {},
                        takeRecords: function() { return []; }
                    };
                };
                
                // Add IntersectionObserver stub
                window.IntersectionObserver = window.IntersectionObserver || function() {
                    return {
                        observe: function() {},
                        unobserve: function() {},
                        disconnect: function() {}
                    };
                };
                
                // Add ResizeObserver stub
                window.ResizeObserver = window.ResizeObserver || function() {
                    return {
                        observe: function() {},
                        unobserve: function() {},
                        disconnect: function() {}
                    };
                };
                
                // CustomEvent
                window.CustomEvent = window.CustomEvent || function(type, params) {
                    params = params || { bubbles: false, cancelable: false, detail: null };
                    var evt = document.createEvent ? document.createEvent('CustomEvent') : {};
                    evt.type = type;
                    evt.bubbles = params.bubbles;
                    evt.cancelable = params.cancelable;
                    evt.detail = params.detail;
                    return evt;
                };
                
                // Add btoa/atob
                window.btoa = window.btoa || function(str) {
                    // Simplified base64 encode
                    return str; // Would need proper implementation
                };
                
                window.atob = window.atob || function(str) {
                    // Simplified base64 decode
                    return str; // Would need proper implementation
                };
                
                // Add URL API stub
                window.URL = window.URL || function(url, base) {
                    this.href = url;
                    var parts = url.match(/^(\w+):\/\/([^\/]+)(\/[^\?]*)?(\?[^#]*)?(#.*)?$/);
                    if (parts) {
                        this.protocol = parts[1] + ':';
                        this.host = parts[2];
                        this.pathname = parts[3] || '/';
                        this.search = parts[4] || '';
                        this.hash = parts[5] || '';
                    }
                };
                
                // Add URLSearchParams stub
                window.URLSearchParams = window.URLSearchParams || function(init) {
                    this._params = {};
                    if (typeof init === 'string') {
                        var pairs = init.replace(/^\?/, '').split('&');
                        for (var i = 0; i < pairs.length; i++) {
                            var pair = pairs[i].split('=');
                            if (pair[0]) {
                                this._params[decodeURIComponent(pair[0])] = 
                                    decodeURIComponent(pair[1] || '');
                            }
                        }
                    }
                };
                
                if (window.URLSearchParams) {
                    window.URLSearchParams.prototype.get = function(name) {
                        return this._params[name] || null;
                    };
                    window.URLSearchParams.prototype.set = function(name, value) {
                        this._params[name] = value;
                    };
                    window.URLSearchParams.prototype.has = function(name) {
                        return name in this._params;
                    };
                }
                
                // Anti-headless detection
                // Make sure we don't look like a headless browser
                Object.defineProperty(navigator, 'webdriver', {
                    get: function() { return false; },
                    configurable: true
                });
                
                // Chrome-specific properties
                window.chrome = window.chrome || {
                    runtime: {},
                    loadTimes: function() {},
                    csi: function() {},
                    app: {}
                };
                
                // Add permissions API
                navigator.permissions = navigator.permissions || {
                    query: function() {
                        return Promise.resolve({ state: 'granted' });
                    }
                };
                
                // Add plugins
                if (!navigator.plugins || navigator.plugins.length === 0) {
                    Object.defineProperty(navigator, 'plugins', {
                        get: function() {
                            return [
                                { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer' },
                                { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai' },
                                { name: 'Native Client', filename: 'internal-nacl-plugin' }
                            ];
                        }
                    });
                }
                
                // Add mimeTypes
                if (!navigator.mimeTypes || navigator.mimeTypes.length === 0) {
                    Object.defineProperty(navigator, 'mimeTypes', {
                        get: function() {
                            return [
                                { type: 'application/pdf', suffixes: 'pdf', description: 'Portable Document Format' },
                                { type: 'text/pdf', suffixes: 'pdf', description: 'Portable Document Format' }
                            ];
                        }
                    });
                }
                
                // Make sure window.chrome exists (Google checks this)
                if (!window.chrome) {
                    window.chrome = { runtime: {} };
                }
                
                // Add connection API
                navigator.connection = navigator.connection || {
                    effectiveType: '4g',
                    downlink: 10,
                    rtt: 50,
                    saveData: false
                };
                
                // Add battery API stub
                navigator.getBattery = navigator.getBattery || function() {
                    return Promise.resolve({
                        charging: true,
                        chargingTime: 0,
                        dischargingTime: Infinity,
                        level: 1
                    });
                };
                
                // Add languages
                if (!navigator.languages || navigator.languages.length === 0) {
                    Object.defineProperty(navigator, 'languages', {
                        get: function() { return ['en-US', 'en']; }
                    });
                }
                
                // NodeList forEach
                if (typeof NodeList !== 'undefined' && NodeList.prototype && !NodeList.prototype.forEach) {
                    NodeList.prototype.forEach = Array.prototype.forEach;
                }
                
                // HTMLCollection forEach
                if (typeof HTMLCollection !== 'undefined' && HTMLCollection.prototype && !HTMLCollection.prototype.forEach) {
                    HTMLCollection.prototype.forEach = Array.prototype.forEach;
                }
            }
        ");
    }
}

