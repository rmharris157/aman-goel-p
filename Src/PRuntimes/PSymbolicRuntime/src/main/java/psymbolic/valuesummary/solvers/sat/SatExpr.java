package psymbolic.valuesummary.solvers.sat;

import lombok.Getter;
import psymbolic.runtime.logger.SearchLogger;
import psymbolic.valuesummary.solvers.SolverType;
import psymbolic.valuesummary.solvers.sat.expr.Fraig;
import psymbolic.valuesummary.solvers.sat.expr.ExprLib;
import psymbolic.valuesummary.solvers.sat.expr.ExprLibType;
import psymbolic.valuesummary.solvers.sat.expr.NativeExpr;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;

public class SatExpr {
    public static int numVars = 0;
    public static HashMap<Object, SatObject> table = new HashMap<Object, SatObject>();
    public static HashMap<Object, SatObject> aigTable = new HashMap<Object, SatObject>();
    private Object expr;

    private static ExprLib exprImpl;

    @Getter
    private static ExprLibType exprType;

    public static ExprLib getExprImpl() {
        return exprImpl;
    }

    public static void setExprLib(ExprLibType type) {
        exprType = type;
        switch(type) {
            case Fraig:	            exprImpl = new Fraig();
                break;
            case NativeExpr:	    exprImpl = new NativeExpr();
                break;
            default:
                throw new RuntimeException("Unexpected/incompatible expression library type " + type);
        }
    }

    public SatExpr(Object expr) {
        this.expr = expr;
    }

    private static SatObject createSatFormula(Object original) {
        if (table.containsKey(original)) {
            return table.get(original);
        } else if (SatGuard.getSolverType() == SolverType.ABC) {
            if (aigTable.containsKey(original)) {
                return aigTable.get(original);
            }
            SatObject satFormula = new SatObject(original, SatStatus.Unknown);
            table.put(original, satFormula);
            return satFormula;
        }

        Object expr = original;
//        long expr = Aig.simplify(original);
//        System.out.println("Creating Sat formula for " + toString(expr));


        SatObject satFormula = new SatObject(null, SatStatus.Unknown);
        SatExprType satExprType = getExprImpl().getType(expr);

        List<Object> satChildren = new ArrayList<>();
        for (Object child: getExprImpl().getChildren(expr)) {
            SatObject satChild = createSatFormula(child);
            satChildren.add(satChild.formula);
        }

        switch(satExprType) {
            case TRUE:
                satFormula.formula = SatGuard.getSolver().constTrue();
                satFormula.status = SatStatus.Sat;
                break;
            case FALSE:
                satFormula.formula = SatGuard.getSolver().constFalse();
                satFormula.status = SatStatus.Unsat;
                break;
            case VARIABLE:
                satFormula.formula = SatGuard.getSolver().newVar(getExprImpl().toString(expr));
                break;
            case NOT:
                assert (satChildren.size() == 1);
                satFormula.formula = SatGuard.getSolver().not(satChildren.get(0));
                satFormula.status = SatStatus.Unknown;
                break;
            case AND:
                satFormula.formula = SatGuard.getSolver().and(satChildren);
                satFormula.status = SatStatus.Unknown;
                break;
            case OR:
                satFormula.formula = SatGuard.getSolver().or(satChildren);
                satFormula.status = SatStatus.Unknown;
                break;
            default:
                throw new RuntimeException("Unexpected expr of type " + satExprType + " : " + expr);
        }
        table.put(expr, satFormula);
        return satFormula;
    }

    private static boolean checkSat(SatExpr formula, SatObject satFormula) {
        switch (satFormula.status) {
            case Sat:
                return true;
            case Unsat:
                return false;
            default:
//            	System.out.println("Checking satisfiability of formula: " + Aig.toString(formula.expr));
                boolean isSat = SatGuard.getSolver().isSat(satFormula.formula);
                if (SearchLogger.getVerbosity() > 4) {
                	System.out.println("\t\tSAT ? [ " + getExprImpl().toString(formula.expr) + " ] :\t" + isSat);
                }
//            	System.out.println("Result: " + isSat);
                if (isSat) {
                    satFormula.status = SatStatus.Sat;
                    return true;
                } else {
                    satFormula.status = SatStatus.Unsat;
                    // Also update the sat formula node to FALSE
                    satFormula.formula = SatGuard.getSolver().constFalse();
                    return false;
                }
        }
    }

    private static SatObject createAigFormula(Object original) {
        if (aigTable.containsKey(original)) {
            return aigTable.get(original);
        }
        SatObject satFormula = new SatObject(original, SatStatus.Unknown);
        satFormula.status = ((Fraig)exprImpl).isSat((Long)original, Fraig.nBTLimit);
        aigTable.put(original, satFormula);
        return satFormula;
    }

    public static boolean isSat(SatExpr formula) {
        SatObject satFormula;
//        if (getExprType() == ExprLibType.Aig) {
//            satFormula = createAigFormula(formula.expr);
//            switch (satFormula.status) {
//                case Sat:
//                    return true;
//                case Unsat:
//                    return false;
//                default:
//            }
//        }
        satFormula = createSatFormula(formula.expr);
        return checkSat(formula, satFormula);
    }

    private static SatExpr newExpr(Object original) {
        return new SatExpr(original);
    }

    public static SatExpr ConstTrue() {
        return newExpr(getExprImpl().getTrue());
    }

    public static SatExpr ConstFalse() {
        return newExpr(getExprImpl().getFalse());
    }

    public static SatExpr NewVar() {
        return newExpr(getExprImpl().newVar("x" + numVars++));
    }

    public static SatExpr Not(SatExpr formula) {
        return newExpr(getExprImpl().not(formula.expr));
    }

    public static SatExpr And(SatExpr left, SatExpr right) {
        return newExpr(getExprImpl().and(left.expr, right.expr));
    }

    public static SatExpr Or(SatExpr left, SatExpr right) {
        return newExpr(getExprImpl().or(left.expr, right.expr));
    }

    @Override
    public String toString() {
        return getExprImpl().toString(this.expr);
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (!(o instanceof SatExpr)) return false;
        SatExpr that = (SatExpr) o;
        return getExprImpl().areEqual(this.expr, that.expr);
    }

    @Override
    public int hashCode() {
        return getExprImpl().getHashCode(this.expr);
    }
}
