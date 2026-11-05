// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

export function setup() {
    window.quickGridDisposal = {
        hold: false, inits: 0, completions: 0, disposals: 0,
        pending: [], connectedAccesses: 0, detachedAccesses: 0, probing: false,
        completed() { this.completions++; },
        disposed() { this.disposals++; },
        release() { this.pending.splice(0).forEach(resolve => resolve()); },
        init(table, originalInit) {
            const head = table.tHead;
            const querySelector = head.querySelector;
            this.restore = () => { head.querySelector = querySelector; };
            head.querySelector = (selector) => {
                if (this.probing) {
                    if (table.isConnected) {
                        this.connectedAccesses++;
                    } else {
                        this.detachedAccesses++;
                    }
                }
                return querySelector.call(head, selector);
            };
            const result = originalInit(table);
            this.inits++;
            return this.hold ? new Promise(resolve => this.pending.push(() => resolve(result))) : result;
        },
        probe() {
            this.connectedAccesses = 0;
            this.detachedAccesses = 0;
            this.probing = true;
            try {
                document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
                document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
                document.body.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'Escape' }));
            } finally {
                this.probing = false;
            }
        },
        cleanup() {
            this.release();
            this.restore?.();
        }
    };
}
