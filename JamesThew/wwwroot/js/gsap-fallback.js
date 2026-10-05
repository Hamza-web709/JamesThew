(function () {
    'use strict';

    if (window.gsap && window.ScrollTrigger) {
        return;
    }

    var triggers = [];

    function toArray(targets) {
        if (!targets) {
            return [];
        }

        if (typeof targets === 'string') {
            return Array.prototype.slice.call(document.querySelectorAll(targets));
        }

        if (targets instanceof Element || targets === window || targets === document) {
            return [targets];
        }

        if (Array.isArray(targets)) {
            return targets.reduce(function (items, target) {
                return items.concat(toArray(target));
            }, []);
        }

        if (typeof targets.length === 'number') {
            return Array.prototype.slice.call(targets);
        }

        return [];
    }

    function rememberScrollTrigger(config) {
        if (!config) {
            return;
        }

        var trigger = config.trigger || null;
        if (typeof trigger === 'string') {
            trigger = document.querySelector(trigger);
        }

        triggers.push(Object.assign({}, config, { trigger: trigger }));
    }

    function applyProps(targets, props) {
        toArray(targets).forEach(function (el) {
            if (!el || !el.style || !props) {
                return;
            }

            if (props.clearProps) {
                var names = props.clearProps === 'all'
                    ? ['transform', 'opacity', 'filter', 'clipPath']
                    : props.clearProps.split(',').map(function (name) { return name.trim(); });
                names.forEach(function (name) {
                    if (name) {
                        el.style[name] = '';
                    }
                });
            }

            if (typeof props.opacity !== 'undefined') {
                el.style.opacity = props.opacity;
            }
            if (typeof props.x !== 'undefined' || typeof props.y !== 'undefined' || typeof props.scale !== 'undefined') {
                var x = typeof props.x === 'function' ? props.x() : (props.x || 0);
                var y = typeof props.y === 'function' ? props.y() : (props.y || 0);
                var scale = typeof props.scale === 'undefined' ? 1 : props.scale;
                el.style.transform = 'translate(' + x + 'px, ' + y + 'px) scale(' + scale + ')';
            }
            if (typeof props.filter !== 'undefined') {
                el.style.filter = props.filter;
            }
            if (typeof props.clipPath !== 'undefined') {
                el.style.clipPath = props.clipPath;
            }
        });
    }

    function chain() {
        return {
            to: function (targets, props) {
                if (props && props.scrollTrigger) {
                    rememberScrollTrigger(props.scrollTrigger);
                }
                applyProps(targets, props);
                return this;
            },
            fromTo: function (targets, fromProps, toProps) {
                if (toProps && toProps.scrollTrigger) {
                    rememberScrollTrigger(toProps.scrollTrigger);
                }
                applyProps(targets, toProps);
                return this;
            }
        };
    }

    window.ScrollTrigger = {
        create: function (config) {
            rememberScrollTrigger(config);
            return config;
        },
        getAll: function () {
            return triggers.slice();
        },
        refresh: function () {}
    };

    window.gsap = {
        registerPlugin: function () {},
        matchMedia: function () {
            return {
                add: function (query, callback) {
                    if (!query || !window.matchMedia || window.matchMedia(query).matches) {
                        callback();
                    }
                }
            };
        },
        timeline: function (config) {
            if (config && config.scrollTrigger) {
                rememberScrollTrigger(config.scrollTrigger);
            }
            return chain();
        },
        to: function (targets, props) {
            if (props && props.scrollTrigger) {
                rememberScrollTrigger(props.scrollTrigger);
            }
            applyProps(targets, props);
            return chain();
        },
        fromTo: function (targets, fromProps, toProps) {
            if (toProps && toProps.scrollTrigger) {
                rememberScrollTrigger(toProps.scrollTrigger);
            }
            applyProps(targets, toProps);
            return chain();
        },
        set: applyProps
    };
}());
